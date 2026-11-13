using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches.Black;

public class Aoc : IPatch
{
    public string Name => "Aoc Patch";
    public string Description => "";

    // Client blocks that stay; the rest are emptied.
    private static readonly HashSet<string> Keep =
    [
        "ClientAnimationController",
        // "SoundEvents",
        "BoneGroups",
        // "AnimatedRender",
        // "FixedMesh",
        "SkinMesh",
    ];

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var transform = Utf16.Edit(text => AoFile.StripClientBlocks(text, Keep, leaveEmptyBlocks: true));
        foreach (var file in GameTree.FindFiles(root, ".aoc", ".ao"))
            plan.Add(file, transform);
    }
}
