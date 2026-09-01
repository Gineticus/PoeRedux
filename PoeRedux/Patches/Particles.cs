using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class Particles : IPatch, ISelectiveSweep
{
    public string Name => "Particles Patch";
    public object Description =>
        "Blanks every particle definition, your own skills included. Microtransaction effects are untouched.";

    private List<FileNode> fileNodes = [];

    public IReadOnlyList<string> Extensions { get; } = [
        ".pet",
        ".trl",
    ];

    public HashSet<string> DisabledPrefixes { get; } = new(StringComparer.Ordinal) {
    // legion particles
        "metadata/particles/monster_effects/league_legion/rewardsystem",
        "metadata/particles/monster_effects/league_legion/endgame",
    // delve particles
        "metadata/particles/monster_effects/league_delve/general",
    // drox, the warlord particles
        "metadata/particles/monster_effects/atlasexiles/adjudicator",
        "metadata/particles/monster_effects/atlasexiles/adjudicatormonsters",
    // guardian of the chimera particles
        "metadata/particles/enviro_effects/act3/blood_temple",
        "metadata/particles/ground_effects_v2/smoke_blind_chimera",
        "metadata/particles/monster_effects/atlasofworldsbosses/chimera",
    // sirus, awakener of worlds particles
        "metadata/particles/monster_effects/atlasexiles/orion",
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

        if (DisabledPrefixes.Any(p => record.Path.Replace('\\', '/').StartsWith(p, StringComparison.Ordinal)))
            return;

        var newBytes = Encoding.Unicode.GetBytes("0");
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
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
        var dir = NavigateTo(root, "metadata", "particles");
        if (dir is null)
        {
            PatchReport.FileMissing(Scope);
            return;
        }
        CollectFileNodesRecursively(dir);

        if (fileNodes.Count == 0)
        {
            PatchReport.NoMatch(Scope, string.Join(" or ", Extensions) + " files");
            return;
        }

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }

    public string Root => Scope;

    private const string Scope = "metadata/particles";
}