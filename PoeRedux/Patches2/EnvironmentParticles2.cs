using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

/// <summary>
/// The PoE 2 version of <see cref="EnvironmentParticles"/>, which also turns off clouds and rain.
/// </summary>
public class EnvironmentParticles2 : IPatch
{
    public string Name => "Environment Particles Patch";
    public string Description => "Disables the default environment particles in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) => EnvironmentSettings.EditAll(root, plan, Edit);

    private static string Edit(string text)
    {
        text = EnvironmentSettings.DisableKeys(text, "area", "fog", "screenspace_fog", "effect_spawner", "post_processing");
        text = EnvironmentSettings.SetToZero(text, "clouds_intensity");
        return EnvironmentSettings.SetToZero(text, "rain_intensity");
    }
}
