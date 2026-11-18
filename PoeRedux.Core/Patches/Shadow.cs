using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Shadow : IPatch
{
    public string Id => "shadow";
    public string Name => "Shadows";
    public string Description => "No shadows";
    public PatchGroup Group => PatchGroup.VisualNoise;

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        EnvironmentSettings.EditAll(root, plan, EnvironmentSettings.DisableShadows);
}
