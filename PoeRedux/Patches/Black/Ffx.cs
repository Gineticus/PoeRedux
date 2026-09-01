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

    private bool TryPatchFile(FileNode file)
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

        if (result == data)
            return false;

        var newBytes = Encoding.Unicode.GetBytes(result);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
    }

    private bool HasTargetExtension(string fileName) =>
        extensions.Any(ext =>
            fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public void Apply(DirectoryNode root)
    {
        CollectFileNodesRecursively(root);

        var changed = 0;
        foreach (var file in fileNodes)
            if (TryPatchFile(file)) changed++;

        if (changed == 0)
            PatchReport.NoMatch("the game files", "a FRAGMENT body in " + string.Join(" or ", extensions));
    }
}