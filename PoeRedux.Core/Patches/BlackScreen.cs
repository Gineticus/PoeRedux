using LibBundle3.Nodes;
using PoeRedux.Patches.Black;

namespace PoeRedux.Patches;

public class BlackScreen : IPatch
{
    public string Id => "black-screen";
    public string Name => "Black Screen";
    public string Description => "Black game screen, experimental";
    public PatchGroup Group => PatchGroup.Experimental;

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
