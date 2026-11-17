using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches.Black;

public class Pet : IPatch
{
    public string Id => "black-pet";
    public string Name => "Pet Patch";
    public PatchGroup Group => PatchGroup.Experimental;
    public string Description => "";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".pet", ".trl"))
            plan.Add(file, _ => Utf16.WithBom("0"));
    }
}
