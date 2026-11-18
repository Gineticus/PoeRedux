using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Particles : FolderSweepPatch
{
    private static readonly FileTransform Blank = _ => Utf16.WithBom("0");

    public override string Id => "particles";
    public override string Name => "Particles";
    public override string Description => "No particle effects";
    public override PatchGroup Group => PatchGroup.VisualNoise;
    public override string Root => "metadata/particles";
    public override IReadOnlyList<string> Extensions { get; } = [".pet", ".trl"];

    // League mechanics and bosses whose particles show what to do or what to avoid.
    public override IReadOnlyList<string> DefaultKept { get; } =
    [
        // Legion
        "metadata/particles/monster_effects/league_legion/rewardsystem",
        "metadata/particles/monster_effects/league_legion/endgame",
        // Delve
        "metadata/particles/monster_effects/league_delve/general",
        // Drox, the Warlord
        "metadata/particles/monster_effects/atlasexiles/adjudicator",
        "metadata/particles/monster_effects/atlasexiles/adjudicatormonsters",
        // Guardian of the Chimera
        "metadata/particles/enviro_effects/act3/blood_temple",
        "metadata/particles/ground_effects_v2/smoke_blind_chimera",
        "metadata/particles/monster_effects/atlasofworldsbosses/chimera",
        // Sirus, Awakener of Worlds
        "metadata/particles/monster_effects/atlasexiles/orion",
    ];

    protected override FileTransform TransformFor(FileNode file) => Blank;
}

public class Effects : FolderSweepPatch
{
    // Client blocks that stay; the rest are removed.
    internal static readonly HashSet<string> KeepBlocks =
    [
        "ClientAnimationController",
        "SoundEvents",
        "BoneGroups",
        "AnimatedRender",
        "SkinMesh",
    ];

    private static readonly FileTransform Strip = Utf16.Edit(text => AoFile.StripClientBlocks(text, KeepBlocks, leaveEmptyBlocks: false));

    public override string Id => "effects";
    public override string Name => "Effects";
    public override string Description => "No skill and spell effects";
    public override PatchGroup Group => PatchGroup.VisualNoise;
    public override string Root => "metadata/effects/spells";
    public override IReadOnlyList<string> Extensions { get; } = [".aoc", ".ao"];

    // League mechanics, bosses and dangerous ground: the effect is the warning.
    public override IReadOnlyList<string> DefaultKept { get; } =
    [
        // Expedition
        "metadata/effects/spells/monsters_effects/league_expedition/dynamic_marker",
        // Atlas bosses, the Maven included
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses",
        // Azmeri
        "metadata/effects/spells/monsters_effects/league_azmeri/guiding_light",
        "metadata/effects/spells/monsters_effects/league_azmeri/monster_fx",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/affecting_area",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/feature_room_dust",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/guiding_light",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/wisp_doodads",
        // Legion
        "metadata/effects/spells/monsters_effects/league_legion/rewardsystem",
        // Blight
        "metadata/effects/spells/monsters_effects/league_blight/rewardsystem",
        // Archnemesis, Ritual and Ultimatum
        "metadata/effects/spells/monsters_effects/league_archnemesis",
        "metadata/effects/spells/monsters_effects/league_ritual/cold_ritual",
        "metadata/effects/spells/monsters_effects/league_ultimatum/mechanics/fx/arena_limit.pet",
        // Sanctum
        "metadata/effects/spells/monsters_effects/league_sanctum",
        "metadata/effects/spells/monsters_effects/league_hellscape/mechanics",
        // Drox, the Warlord. Its prefix also keeps adjudicatormonsters.
        "metadata/effects/spells/monsters_effects/atlasexiles/adjudicator",
        // Guardian of the Chimera
        "metadata/effects/spells/ground_effects/chimera_smoke",
        "metadata/effects/spells/ground_effects/evil",
        "metadata/effects/spells/ground_effects_v2/smoke_blind_chimera",
        // Sirus, Awakener of Worlds
        "metadata/effects/spells/monsters_effects/atlasexiles/orion",
        // Prophecy
        "metadata/effects/spells/monsters_effects/prophecy_league",
        // Dangerous ground
        "metadata/effects/spells/ground_effects/caustic",
        "metadata/effects/spells/ground_effects_v2/caustic_arrow_ground",
        "metadata/effects/spells/ground_effects_v2/desecrated",
        "metadata/effects/spells/ground_effects_v2/desecrated_maligaro",
        "metadata/effects/spells/ground_effects_v2/desecrated_red",
        "metadata/effects/spells/ground_effects_v3/caustic",
    ];

    protected override FileTransform TransformFor(FileNode file) => Strip;
}

/// <summary>
/// Removes microtransaction effects: auras, weapon and armour effects, pets, wings, on-kill effects. Portals,
/// map devices and waypoints stay, so that what you click on stays visible.
/// </summary>
public class Mtx : FolderSweepPatch
{
    private static readonly FileTransform Blank = _ => Utf16.WithBom("0");
    private static readonly FileTransform Strip = Utf16.Edit(text => AoFile.StripClientBlocks(text, Effects.KeepBlocks, leaveEmptyBlocks: false));

    public override string Id => "mtx";
    public override string Name => "Microtransactions";
    public override string Description => "No shop cosmetic effects";
    public override PatchGroup Group => PatchGroup.VisualNoise;
    public override string Root => "metadata/effects/microtransactions";
    public override IReadOnlyList<string> Extensions { get; } = [".pet", ".trl", ".ao", ".aoc"];

    // Checked in both games on 18 Nov 2026: these folders hold the portal, map device and waypoint effects.
    public override IReadOnlyList<string> DefaultKept { get; } =
    [
        "metadata/effects/microtransactions/town_portals",
        "metadata/effects/microtransactions/hideout/mapdevice",
        "metadata/effects/microtransactions/misc/mapdevice",
        "metadata/effects/microtransactions/waypoint",
    ];

    protected override FileTransform TransformFor(FileNode file) =>
        file.Name.EndsWith(".ao", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".aoc", StringComparison.OrdinalIgnoreCase)
            ? Strip
            : Blank;
}
