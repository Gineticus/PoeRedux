using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

/// <summary>
/// Turns off weather and atmosphere layers in every area's .env file. Replaces the separate Fog, Rain, Clouds
/// and Environment Particles patches: each became a switch here.
/// </summary>
public class EnvironmentLayers : SwitchPatch
{
    public override string Id => "environment";
    public override string Name => "Environment";
    public override string Description => "Turns off fog, rain, clouds and other weather layers.";
    public override PatchGroup Group => PatchGroup.VisualNoise;

    // Every key below occurs in the .env files of both games (checked on 18 Nov 2026), and only as a key.
    public override IReadOnlyList<PatchSwitch> Switches { get; } =
    [
        new("fog", "Fog", t => EnvironmentSettings.DisableKeys(t, "fog")),
        new("screenspace_fog", "Screen fog", t => EnvironmentSettings.DisableKeys(t, "screenspace_fog")),
        new("area", "Area layer", t => EnvironmentSettings.DisableKeys(t, "area")),
        new("effect_spawner", "Ambient effects", t => EnvironmentSettings.DisableKeys(t, "effect_spawner")),
        new("post_processing", "Post-processing layer", t => EnvironmentSettings.DisableKeys(t, "post_processing")),
        new("rain_intensity", "Rain", t => EnvironmentSettings.SetToZero(t, "rain_intensity")),
        new("clouds_intensity", "Clouds", t => EnvironmentSettings.SetToZero(t, "clouds_intensity")),
    ];


    public override void Collect(DirectoryNode root, PatchPlan plan)
    {
        if (OnSwitches.Any())
            EnvironmentSettings.EditAll(root, plan, ApplySwitches);
    }
}

/// <summary>Turns off light layers in every area's .env file.</summary>
public class Light : SwitchPatch
{
    public override string Id => "light";
    public override string Name => "Light";
    public override string Description => "Turns off sun, character and reflected light.";
    public override PatchGroup Group => PatchGroup.VisualNoise;

    public override IReadOnlyList<PatchSwitch> Switches { get; } =
    [
        new("directional_light", "Sun and moon light", t => EnvironmentSettings.DisableKeys(t, "directional_light")),
        new("player_light", "Light around your character", t => EnvironmentSettings.DisableKeys(t, "player_light")),
        new("environment_mapping", "Reflections", t => EnvironmentSettings.DisableKeys(t, "environment_mapping")),
        new("global_illumination", "Bounced light", t => EnvironmentSettings.DisableKeys(t, "global_illumination")),
    ];

    public override void Collect(DirectoryNode root, PatchPlan plan)
    {
        if (OnSwitches.Any())
            EnvironmentSettings.EditAll(root, plan, ApplySwitches);
    }
}
