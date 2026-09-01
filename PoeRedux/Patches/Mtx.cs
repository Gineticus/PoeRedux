using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class Mtx : IPatch, ISelectiveSweep
{
    public string Name => "MTX Patch";
    public object Description =>
        "Strips microtransaction visuals: auras, weapon effects, pets, wings, on-kill effects. " +
        "Your own MTX is stripped too; untick its folder in MTX Folders to keep it. " +
        "Town portals are kept by default so portals stay visible.";

    private List<FileNode> fileNodes = [];

    public IReadOnlyList<string> Extensions { get; } = [
        ".pet",
        ".trl",
        ".ao",
        ".aoc",
    ];

    public HashSet<string> DisabledPrefixes { get; } = new(StringComparer.Ordinal) {
    // an MTX portal is the portal's whole visual, blanking it risks a portal nobody can see
        "metadata/effects/microtransactions/town_portals",
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
        var path = record.Path.Replace('\\', '/');

        if (DisabledPrefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
            return false;

        byte[] newBytes;
        if (path.EndsWith(".ao", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".aoc", StringComparison.OrdinalIgnoreCase))
        {
            var data = Encoding.Unicode.GetString(record.Read().ToArray());
            if (string.IsNullOrEmpty(data))
                return false;

            var patched = Effects.StripClientBlocks(data, Effects.ClientKeep);
            if (patched == data)
                return false;
            newBytes = Encoding.Unicode.GetBytes(patched);
        }
        else
        {
            newBytes = Encoding.Unicode.GetBytes("0");
        }

        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
    }

    private bool HasTargetExtension(string fileName) =>
        Extensions.Any(ext =>
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
        var dir = NavigateTo(root, "metadata", "effects", "microtransactions");
        if (dir is null)
        {
            PatchReport.FileMissing(Scope);
            return;
        }
        CollectFileNodesRecursively(dir);

        var changed = 0;
        foreach (var file in fileNodes)
            if (TryPatchFile(file)) changed++;

        if (changed == 0)
            PatchReport.NoMatch(Scope, string.Join(" or ", Extensions) + " files outside the kept folders");
    }

    public string Root => Scope;

    private const string Scope = "metadata/effects/microtransactions";
}
