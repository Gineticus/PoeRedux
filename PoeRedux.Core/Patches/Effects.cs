using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Effects : IPatch
{
    public string Name => "Effects Patch";
    public string Description => "Disables all effects in the game.";

    // Client blocks that stay; the rest are removed.
    private static readonly HashSet<string> Keep =
    [
        "ClientAnimationController",
        "SoundEvents",
        "BoneGroups",
        "AnimatedRender",
        "SkinMesh",
    ];

    // Effects of league mechanics, bosses and dangerous ground stay as they are.
    private static readonly string[] Protected =
    [
    // expedition effects
        "metadata/effects/spells/monsters_effects/league_expedition/dynamic_marker",
    // world bosses effects
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses",
    // affliction effects
        "metadata/effects/spells/monsters_effects/league_azmeri/guiding_light",
        "metadata/effects/spells/monsters_effects/league_azmeri/monster_fx",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/affecting_area",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/feature_room_dust",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/guiding_light",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/wisp_doodads",
    // legion effects
        "metadata/effects/spells/monsters_effects/league_legion/rewardsystem",
    // blight effects
        "metadata/effects/spells/monsters_effects/league_blight/rewardsystem",
    // ultimatum effects
        "metadata/effects/spells/monsters_effects/league_archnemesis",
        "metadata/effects/spells/monsters_effects/league_ritual/cold_ritual",
        "metadata/effects/spells/monsters_effects/league_ultimatum/mechanics/fx/arena_limit.pet",
    // sanctum effects
        "metadata/effects/spells/monsters_effects/league_sanctum",
        "metadata/effects/spells/monsters_effects/league_hellscape/mechanics",
    // maven effects
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses/maven",
    // drox, the warlord effects
        "metadata/effects/spells/monsters_effects/atlasexiles/adjudicator",
        // "metadata/effects/spells/monsters_effects/atlasexiles/adjudicatormonsters",
    // guardian of the chimera effects
        "metadata/effects/spells/ground_effects/chimera_smoke",
        "metadata/effects/spells/ground_effects/evil",
        "metadata/effects/spells/ground_effects_v2/smoke_blind_chimera",
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses/chimera",
    // sirus, awakener of worlds effects
        "metadata/effects/spells/monsters_effects/atlasexiles/orion",
    // prophecy effects
        "metadata/effects/spells/monsters_effects/prophecy_league",
    // deadly ground effects
        "metadata/effects/spells/ground_effects/caustic",
        "metadata/effects/spells/ground_effects_v2/caustic_arrow_ground",
        "metadata/effects/spells/ground_effects_v2/desecrated",
        "metadata/effects/spells/ground_effects_v2/desecrated_maligaro",
        "metadata/effects/spells/ground_effects_v2/desecrated_red",
        "metadata/effects/spells/ground_effects_v3/caustic",
    ];

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var transform = Utf16.Edit(text => AoFile.StripClientBlocks(text, Keep, leaveEmptyBlocks: false));
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, "metadata/effects/spells"), ".aoc", ".ao"))
        {
            if (!Protected.Any(p => file.Record.Path.Replace('\\', '/').StartsWith(p, StringComparison.Ordinal)))
                plan.Add(file, transform);
        }
    }
}
