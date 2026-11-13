using LibBundle3.Nodes;

namespace PoeRedux.Patches;

public class Clouds : IPatch
{
    public string Name => "Clouds Patch";
    public string Description => "Disables the default clouds effect in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, text => EnvironmentSettings.SetToZero(text, "clouds_intensity"));
}
