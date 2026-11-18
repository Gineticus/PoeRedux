using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

/// <summary>
/// Hides every element of the loading screens: the area picture, the tip, the area name and the black frame
/// behind them. "visible = false;" and "mouse_enabled = false;" are properties the game's own .ui files use.
/// </summary>
public class LoadingScreen : IPatch
{
    public string Id => "loading-screen";
    public string Name => "Loading Screen";
    public string Description => "No picture or tip while loading";
    public PatchGroup Group => PatchGroup.VisualNoise;

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var transform = Utf16.Edit(Hide);
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, "metadata/ui/loadingstate"), ".ui"))
            plan.Add(file, transform);
    }

    internal static string? Hide(string text)
    {
        var lines = text.Split("\r\n").ToList();
        var changed = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].TrimStart().StartsWith("begin ", StringComparison.Ordinal))
                continue;
            // Already hidden by an earlier apply.
            if (i + 1 < lines.Count && lines[i + 1].Trim() == "visible = false;")
                continue;
            var indent = new string('\t', lines[i].Length - lines[i].TrimStart('\t').Length + 1);
            lines.Insert(i + 1, indent + "mouse_enabled = false;");
            lines.Insert(i + 1, indent + "visible = false;");
            changed = true;
        }
        return changed ? string.Join("\r\n", lines) : null;
    }
}
