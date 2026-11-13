using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Light : IPatch
{
    public string Name => "Light Patch";
    public string Description => "Disables the default light effect in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, text => EnvironmentSettings.DisableKeys(text,
            "directional_light", "player_light", "environment_mapping", "global_illumination"));
}
