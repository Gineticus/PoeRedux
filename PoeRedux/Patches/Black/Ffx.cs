using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches.Black;

public class Ffx : IPatch
{
    public string Name => "Ffx Patch";
    public object Description => "";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".ffx",
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

        string pattern = @"(FRAGMENT\s+\w+.*?\{\{)(.*?)(\}\})";

        string result = Regex.Replace(
            data,
            pattern,
            m => $"{m.Groups[1].Value} {m.Groups[3].Value}",
            RegexOptions.Singleline);

        var newBytes = Encoding.Unicode.GetBytes(result);
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