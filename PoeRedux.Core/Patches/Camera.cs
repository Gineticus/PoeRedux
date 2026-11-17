using LibBundle3.Nodes;
using PoeRedux.Helpers;
using System.Globalization;
using System.Text;

namespace PoeRedux.Patches;

public class Camera : IPatch
{
    public string Name => "Camera Patch";
    public string Description => "Allows adjusting the default camera zoom level.";

    public double ZoomLevel { get; set; } = 2.4;

    // Calls that move or zoom the camera. They are removed from every .ot and .otc file except character.ot.
    private static readonly string[] CameraFunctions =
    [
        "CreateCameraZoomNode",
        "ClearCameraZoomNodes",
        "CreateCameraLookAtNode",
        "CreateCameraPanNode",
        "ClearCameraPanNode",
        "ClearCameraPanNodes",
        "SetCustomCameraSpeed",
        "RemoveCustomCameraSpeed",
        "FaceCamera",
    ];

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var character = GameTree.FindFile(root, "metadata/characters/character.ot");
        if (character is not null)
        {
            string zoomLevel = ZoomLevel.ToString(CultureInfo.InvariantCulture);
            plan.Add(character, bytes => PatchCharacterFile(bytes, zoomLevel));
        }

        var removeCalls = Utf16.Edit(text => CameraFunctions.Any(f => text.Contains(f)) ? RemoveCameraFunctions(text) : null);
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, "metadata"), ".ot", ".otc"))
        {
            if (file.Name != "character.ot")
                plan.Add(file, removeCalls);
        }
    }

    // Sets the default zoom: replaces the line with CreateCameraZoomNode, or adds one after "team = 1".
    private static byte[] PatchCharacterFile(byte[] bytes, string zoomLevel)
    {
        string data = Encoding.Unicode.GetString(bytes);
        List<string> lines = data.Split("\r\n").ToList();
        string zoomLine = $"\ton_initial_position_set = {{CreateCameraZoomNode(5000.0, 5000.0, {zoomLevel});}} ";

        if (data.Contains("CreateCameraZoomNode"))
        {
            int x = lines.FindIndex(line => line.Contains("CreateCameraZoomNode"));
            lines[x] = zoomLine;
        }
        else
        {
            int index = lines.FindIndex(x => x.Contains("team = 1"));
            if (index != -1)
                lines.Insert(index + 1, zoomLine);
        }
        return Encoding.Unicode.GetBytes(string.Join("\r\n", lines));
    }

    private static string RemoveCameraFunctions(string data)
    {
        foreach (var func in CameraFunctions)
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
}
