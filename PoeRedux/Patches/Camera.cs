using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class Camera : IPatch
{
    public string Name => "Camera Patch";
    public object Description => "Allows adjusting the default camera zoom level.";

    public double ZoomLevel { get; set; } = 2.4;

    private List<FileNode> fileNodes = [];

    private readonly string[] _extensions = {
        ".ot",
        ".otc",
    };

    private readonly HashSet<string> _functions = new(StringComparer.Ordinal) {
        "CreateCameraZoomNode",
        "ClearCameraZoomNodes",
        "CreateCameraLookAtNode",
        "CreateCameraPanNode",
        "ClearCameraPanNode",
        "ClearCameraPanNodes",
        "SetCustomCameraSpeed",
        "RemoveCustomCameraSpeed",
        "FaceCamera"
    };
    
    private string RemoveCameraFunctions(string data)
    {
        foreach (var func in _functions)
        {
            int pos = 0;
            while ((pos = data.IndexOf(func, pos, StringComparison.Ordinal)) != -1)
            {
                // Check if this is actually a function call (optionally preceded by identifier.)
                int start = pos;
                
                // Look backwards for optional prefix (like "camera_controller.")
                while (start > 0 && (char.IsLetterOrDigit(data[start - 1]) || data[start - 1] == '_' || data[start - 1] == '.'))
                {
                    start--;
                }
                
                // Skip whitespace after function name
                int parenPos = pos + func.Length;
                while (parenPos < data.Length && char.IsWhiteSpace(data[parenPos]))
                {
                    parenPos++;
                }
                
                // Check if followed by opening parenthesis
                if (parenPos >= data.Length || data[parenPos] != '(')
                {
                    pos++;
                    continue;
                }
                
                // Find matching closing parenthesis
                int depth = 1;
                int endPos = parenPos + 1;
                while (endPos < data.Length && depth > 0)
                {
                    if (data[endPos] == '(') depth++;
                    else if (data[endPos] == ')') depth--;
                    endPos++;
                }
                
                if (depth != 0)
                {
                    pos++;
                    continue; // Unmatched parentheses
                }
                
                // Skip whitespace and check for semicolon
                while (endPos < data.Length && char.IsWhiteSpace(data[endPos]))
                {
                    endPos++;
                }
                
                if (endPos < data.Length && data[endPos] == ';')
                {
                    endPos++; // Include the semicolon
                    data = data.Remove(start, endPos - start); // Remove the entire function call
                    pos = start; // Continue from where we removed
                }
                else
                {
                    pos++;
                }
            }
        }
        return data;
    }

    private void CollectFileNodesRecursively(DirectoryNode dir)
    {
        foreach (var node in dir.Children)
        {
            switch (node)
            {
                case DirectoryNode childDir:
                    CollectFileNodesRecursively(childDir);
                    break;

                case FileNode fileNode:
                    if (HasTargetExtension(fileNode.Name) && fileNode.Name != "character.ot")
                        fileNodes.Add(fileNode);
                    break;
            }
        }
    }

    private void TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return;

        if (!_functions.Any(func => data.Contains(func)))
            return;

        data = RemoveCameraFunctions(data);

        var newBytes = Encoding.Unicode.GetBytes(data);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
    }

    private bool HasTargetExtension(string fileName) =>
        _extensions.Any(ext =>
            fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    public void Apply(DirectoryNode root)
    {
        var metadata = NavigateTo(root, "metadata");
        if (metadata is null)
        {
            PatchReport.FileMissing("metadata");
            return;
        }

        CollectFileNodesRecursively(metadata);

        SetDefaultZoom(metadata);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }

    private void SetDefaultZoom(DirectoryNode metadata)
    {
        var characterFile = NavigateTo(metadata, "characters")
            ?.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "character.ot");
        if (characterFile is null)
        {
            PatchReport.FileMissing("metadata/characters/character.ot");
            return;
        }

        var record = characterFile.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());
        List<string> lines = data.Split("\r\n").ToList();
        string zoomLevelString = ZoomLevel.ToString().Replace(',', '.');
        var zoomNode = $"\ton_initial_position_set = {{CreateCameraZoomNode(5000.0, 5000.0, {zoomLevelString});}} ";

        if (data.Contains("CreateCameraZoomNode"))
        {
            int x = lines.FindIndex(line => line.Contains("CreateCameraZoomNode"));
            lines[x] = zoomNode;
        }
        else
        {
            int index = lines.FindIndex(x => x.Contains(PlayerAnchor));
            if (index == -1)
            {
                PatchReport.AnchorMissing("character.ot", PlayerAnchor);
                return;
            }
            lines.Insert(index + 1, zoomNode);
        }
        string newData = string.Join("\r\n", lines);
        var newBytes = Encoding.Unicode.GetBytes(newData);
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
    }

    private const string PlayerAnchor = "team = 1";
}