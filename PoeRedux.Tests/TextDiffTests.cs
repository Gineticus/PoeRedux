using PoeRedux.Services;

namespace PoeRedux.Tests;

public class TextDiffTests
{
    private static string Render(List<TextDiff.DiffLine> lines) => string.Join("\n", lines.Select(l =>
        l.Kind switch
        {
            TextDiff.LineKind.Same => "  " + l.Text,
            TextDiff.LineKind.Removed => "- " + l.Text,
            TextDiff.LineKind.Added => "+ " + l.Text,
            _ => throw new InvalidOperationException(),
        }));

    private static void AssertRebuilds(string[] oldLines, string[] newLines, List<TextDiff.DiffLine> diff)
    {
        var rebuiltOld = diff.Where(l => l.Kind != TextDiff.LineKind.Added).Select(l => l.Text).ToArray();
        var rebuiltNew = diff.Where(l => l.Kind != TextDiff.LineKind.Removed).Select(l => l.Text).ToArray();
        Assert.Equal(oldLines, rebuiltOld);
        Assert.Equal(newLines, rebuiltNew);
    }

    [Fact]
    public void IdenticalFiles_EverythingIsSame()
    {
        string[] lines = ["a", "b", "c"];
        var diff = TextDiff.Lines(lines, lines)!;

        Assert.All(diff, l => Assert.Equal(TextDiff.LineKind.Same, l.Kind));
        AssertRebuilds(lines, lines, diff);
    }

    [Fact]
    public void BothEmpty_EmptyDiff()
    {
        var diff = TextDiff.Lines([], [])!;
        Assert.Empty(diff);
    }

    [Fact]
    public void OldEmpty_EverythingAdded()
    {
        string[] newLines = ["x", "y"];
        var diff = TextDiff.Lines([], newLines)!;

        Assert.All(diff, l => Assert.Equal(TextDiff.LineKind.Added, l.Kind));
        AssertRebuilds([], newLines, diff);
    }

    [Fact]
    public void NewEmpty_EverythingRemoved()
    {
        string[] oldLines = ["x", "y"];
        var diff = TextDiff.Lines(oldLines, [])!;

        Assert.All(diff, l => Assert.Equal(TextDiff.LineKind.Removed, l.Kind));
        AssertRebuilds(oldLines, [], diff);
    }

    [Fact]
    public void PureInsertion_InTheMiddle()
    {
        string[] oldLines = ["a", "b", "c"];
        string[] newLines = ["a", "NEW", "b", "c"];
        var diff = TextDiff.Lines(oldLines, newLines)!;

        AssertRebuilds(oldLines, newLines, diff);
        Assert.Contains(diff, l => l.Kind == TextDiff.LineKind.Added && l.Text == "NEW");
        Assert.Equal(3, diff.Count(l => l.Kind == TextDiff.LineKind.Same));
    }

    [Fact]
    public void PureDeletion_InTheMiddle()
    {
        string[] oldLines = ["a", "b", "c"];
        string[] newLines = ["a", "c"];
        var diff = TextDiff.Lines(oldLines, newLines)!;

        AssertRebuilds(oldLines, newLines, diff);
        Assert.Contains(diff, l => l.Kind == TextDiff.LineKind.Removed && l.Text == "b");
    }

    [Fact]
    public void SingleLineReplacement_LooksLikeCompactModLines()
    {
        // Mirrors what ClientStrings/ModTierColors actually produce: one line's text changes,
        // everything around it is untouched.
        string[] oldLines = ["header", "Prefix Modifier \"{0}\"", "footer"];
        string[] newLines = ["header", "P \"{0}\"", "footer"];
        var diff = TextDiff.Lines(oldLines, newLines)!;

        AssertRebuilds(oldLines, newLines, diff);
        Assert.Equal(2, diff.Count(l => l.Kind == TextDiff.LineKind.Same));
        Assert.Single(diff, l => l.Kind == TextDiff.LineKind.Removed);
        Assert.Single(diff, l => l.Kind == TextDiff.LineKind.Added);
    }

    [Fact]
    public void LargeFile_SmallEditDistance_StaysCorrectAndFast()
    {
        // Mirrors the real worst case this type exists for: stat_descriptions.txt is 444,299
        // lines in the live game data, and ColorMods/ModTierColors touch a small fraction of it.
        var oldLines = Enumerable.Range(0, 50_000).Select(i => $"line {i}").ToArray();
        var newLines = (string[])oldLines.Clone();
        newLines[10_000] = "line 10000 CHANGED";
        newLines[30_000] = "line 30000 CHANGED";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var diff = TextDiff.Lines(oldLines, newLines);
        sw.Stop();

        Assert.NotNull(diff);
        AssertRebuilds(oldLines, newLines, diff!);
        Assert.Equal(2, diff!.Count(l => l.Kind == TextDiff.LineKind.Removed));
        Assert.Equal(2, diff.Count(l => l.Kind == TextDiff.LineKind.Added));
        Assert.True(sw.ElapsedMilliseconds < 5000, $"took {sw.ElapsedMilliseconds}ms for a 4-line edit distance");
    }

    [Fact]
    public void EditDistanceOverCap_ReturnsNull()
    {
        // Every line differs: edit distance is the full length, well past MaxEditDistance.
        var oldLines = Enumerable.Range(0, TextDiff.MaxEditDistance + 100).Select(i => $"old {i}").ToArray();
        var newLines = Enumerable.Range(0, TextDiff.MaxEditDistance + 100).Select(i => $"new {i}").ToArray();

        var diff = TextDiff.Lines(oldLines, newLines);

        Assert.Null(diff);
    }
}
