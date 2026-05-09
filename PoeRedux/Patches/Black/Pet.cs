using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches.Black;

public class Pet : IPatch
{
    public string Name => "Pet Patch";
    public object Description => "";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".pet",
        ".trl",
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

        var newBytes = Encoding.Unicode.GetBytes("0");
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