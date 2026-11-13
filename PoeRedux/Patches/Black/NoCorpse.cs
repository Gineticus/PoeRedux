using LibBundle3.Nodes;

namespace PoeRedux.Patches.Black;

/// <summary>
/// <see cref="Corpse"/>, except that monsters stop rendering the moment they die.
/// </summary>
public class NoCorpse : IPatch
{
    public string Name => "NoCorpse Patch";
    public string Description => "";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        Corpse.HideCorpses(root, plan, onDeath: "{ DisableRendering(); }");
}
