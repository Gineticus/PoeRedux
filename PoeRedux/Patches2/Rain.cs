using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Rain : IPatch
{
    public string Name => "Rain Patch";
    public string Description => "Disables the default rain effect in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, text => EnvironmentSettings.SetToZero(text, "rain_intensity"));
}
