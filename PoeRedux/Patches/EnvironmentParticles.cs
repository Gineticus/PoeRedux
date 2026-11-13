using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class EnvironmentParticles : IPatch
{
    public string Name => "Environment Particles Patch";
    public string Description => "Disables the default environment particles in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, text => EnvironmentSettings.DisableKeys(text,
            "area", "fog", "screenspace_fog", "effect_spawner", "post_processing"));
}
