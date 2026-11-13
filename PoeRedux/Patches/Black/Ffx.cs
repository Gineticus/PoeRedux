using LibBundle3.Nodes;
using PoeRedux.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches.Black;

public class Ffx : IPatch
{
    public string Name => "Ffx Patch";
    public string Description => "";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".ffx"))
            plan.Add(file, bytes => Utf16.WithBom(EmptyFragments(Encoding.Unicode.GetString(bytes))));
    }

    // FRAGMENT name ... {{ code }} becomes FRAGMENT name ... {{ }}.
    private static string EmptyFragments(string text) => Regex.Replace(
        text,
        @"(FRAGMENT\s+\w+.*?\{\{)(.*?)(\}\})",
        m => $"{m.Groups[1].Value} {m.Groups[3].Value}",
        RegexOptions.Singleline);
}
