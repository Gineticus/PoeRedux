using LibBundle3.Nodes;

namespace PoeRedux.Patches;

public interface IPatch
{
    string Name { get; }
    string Description { get; }

    /// <summary>
    /// Adds the files this patch changes to <paramref name="plan"/>. Called once per run with a fresh tree.
    /// The transforms run later on worker threads, so they must not change shared state.
    /// </summary>
    void Collect(DirectoryNode root, PatchPlan plan);
}
