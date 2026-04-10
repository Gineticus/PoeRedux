using LibBundle3.Nodes;
using System.Text;

namespace PoeRedux.Patches;

public class Particles : IPatch
{
    public string Name => "Particles Patch";
    public object Description => "Disables all particle effects in the game.";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".pet",
        ".trl",
    };

    private readonly HashSet<string> _pathProtect = new(StringComparer.Ordinal) {
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

        if (_pathProtect.Any(p => record.Path.Replace('\\', '/').StartsWith(p, StringComparison.Ordinal)))
            return;

        var newBytes = Encoding.Unicode.GetBytes("0");
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
        var dir = NavigateTo(root, "metadata", "particles");
        if (dir is not null)
            CollectFileNodesRecursively(dir);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }
}