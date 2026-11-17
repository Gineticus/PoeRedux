using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches.Black;

public class Epk : IPatch
{
    public string Id => "black-epk";
    public string Name => "Epk Patch";
    public PatchGroup Group => PatchGroup.Experimental;
    public string Description => "";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".epk"))
            plan.Add(file, _ => Utf16.WithBom(""));
    }
}
