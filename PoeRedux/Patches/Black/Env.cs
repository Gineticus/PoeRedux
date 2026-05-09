using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches.Black;

public class Env : IPatch
{
    public string Name => "Env Patch";
    public object Description => "";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".env",
    };

    private readonly string[] _functions = {
        "\"player_light\":",
        "\"environment_mapping\":",
        "\"fog\":",
        "\"screenspace_fog\":",
        "\"area\":",
        "\"water\":",
        "\"post_transform\":",
        "\"audio\":",
        "\"global_illumination\":",
        "\"effect_spawner\":",
        "\"post_processing\":",
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

    private string ReplaceBlockContent(string text, string blockName, string blockContent)
    {
        int nameIndex = text.IndexOf(blockName, StringComparison.Ordinal);
        if (nameIndex < 0)
            return text;

        int openBrace = text.IndexOf('{', nameIndex);
        if (openBrace < 0)
            return text;

        int depth = 0;
        int closeBrace = -1;

        for (int i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;

            if (depth == 0)
            {
                closeBrace = i;
                break;
            }
        }

        if (closeBrace < 0)
            return text;

        return text.Substring(0, openBrace) +
                blockContent +
               text.Substring(closeBrace + 1);
    }

    private void TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        data = data.Replace("\"shadows_enabled\": true", "\"shadows_enabled\": false");

        foreach (var func in _functions)
        {
            data = ReplaceBlockContent(data, func, "{}");
        }

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
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
    }

    private bool HasTargetExtension(string fileName) =>
        extensions.Any(ext =>
            fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public void Apply(DirectoryNode root)
    {
        if (root is not null)
            CollectFileNodesRecursively(root);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }
}