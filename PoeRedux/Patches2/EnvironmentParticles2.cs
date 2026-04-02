using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public class EnvironmentParticles2 : IPatch
{
    public string Name => "Environment Particles Patch";
    public object Description => "Disables the default environment particles in the game.";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".env",
    };

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
                    if (HasTargetExtension(fileNode.Name))
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

        data = data.Replace("\"area\"", "\"xrea\"")
            .Replace("\"fog\"", "\"xog\"")
            .Replace("\"screenspace_fog\"", "\"xcreenspace_fog\"")
            .Replace("\"effect_spawner\"", "\"xffect_spawner\"")
            .Replace("\"post_processing\"", "\"xost_processing\"");

        string pattern = @"(""clouds_intensity"":\s*)([^,\r\n}]+)(,?)";
        string replacement = "${1}0.0${3}";
        data = Regex.Replace(data, pattern, replacement);

        string pattern2 = @"(""rain_intensity"":\s*)([^,\r\n}]+)(,?)";
        string replacement2 = "${1}0.0${3}";
        data = Regex.Replace(data, pattern2, replacement2);

        var newBytes = Encoding.Unicode.GetBytes(data);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        record.Write(newBytes);
    }

    private bool HasTargetExtension(string fileName) =>
        extensions.Any(ext =>
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
        var dir = NavigateTo(root, "metadata", "environmentsettings");
        if (dir is not null)
            CollectFileNodesRecursively(dir);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }
}