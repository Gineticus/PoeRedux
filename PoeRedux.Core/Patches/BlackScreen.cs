using LibBundle3.Nodes;
using PoeRedux.Patches.Black;

namespace PoeRedux.Patches;

public class BlackScreen : IPatch
{
    public string Name => "Black Screen Patch (Experimental)";
    public string Description => "Makes the screen black.";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        new Aoc().Collect(root, plan);
        new Env().Collect(root, plan);
        new Epk().Collect(root, plan);
        new Ffx().Collect(root, plan);
        new Hlsl().Collect(root, plan);
        new Mat().Collect(root, plan);
        new Pet().Collect(root, plan);
        new NoCorpse().Collect(root, plan);
    }
}
