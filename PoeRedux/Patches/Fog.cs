using LibBundle3.Nodes;

namespace PoeRedux.Patches;

public class Fog : IPatch
{
    public string Name => "Fog Patch";
    public string Description => "Disables the default fog effect in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, text => EnvironmentSettings.DisableKeys(text, "fog"));
}
