using LibBundle3.Nodes;

namespace PoeRedux.Patches;

public class Particles : IPatch
{
    public string Name => "Particles Patch";
    public string Description => "Disables all particle effects in the game.";

    // Particles of league mechanics and bosses stay as they are.
    private static readonly string[] Protected =
    [
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
    ];

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, "metadata/particles"), ".pet", ".trl"))
        {
            if (!Protected.Any(p => file.Record.Path.Replace('\\', '/').StartsWith(p, StringComparison.Ordinal)))
                plan.Add(file, _ => Utf16.WithBom("0"));
        }
    }
}
