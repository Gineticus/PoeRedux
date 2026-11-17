using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Shadow : IPatch
{
    public string Name => "Shadow Patch";
    public string Description => "Disables the default shadow effect in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, EnvironmentSettings.DisableShadows);
}
