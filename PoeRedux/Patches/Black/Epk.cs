using LibBundle3.Nodes;

namespace PoeRedux.Patches.Black;

public class Epk : IPatch
{
    public string Name => "Epk Patch";
    public string Description => "";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".epk"))
            plan.Add(file, _ => Utf16.WithBom(""));
    }
}
