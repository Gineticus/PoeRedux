namespace PoeRedux.Services;

/// <summary>
/// A line diff over Myers' O(ND) algorithm, not the textbook O(n*m) LCS table: some files these
/// patches touch run past 400,000 lines (metadata/statdescriptions/stat_descriptions.txt), where
/// an LCS table would be hundreds of billions of cells. Cost here scales with the edit distance,
/// which stays small because these patches make targeted line-level edits, not full rewrites.
/// </summary>
public static class TextDiff
{
    public enum LineKind { Same, Removed, Added }
    public readonly record struct DiffLine(LineKind Kind, string Text);

    /// <summary>
    /// Above this many edited lines, the trace this algorithm keeps (one snapshot per step, up to
    /// the edit distance) would itself become the memory problem it exists to avoid, so the caller
    /// gets told to fall back to a coarse summary instead of a hang.
    /// </summary>
    public const int MaxEditDistance = 20_000;

    public static List<DiffLine>? Lines(string[] oldLines, string[] newLines)
    {
        var n = oldLines.Length;
        var m = newLines.Length;
        var max = n + m;
        if (max == 0) return [];

        var trace = new List<Dictionary<int, int>>();
        var v = new Dictionary<int, int> { [1] = 0 };

        for (var d = 0; d <= max; d++)
        {
            if (d > MaxEditDistance) return null;

            trace.Add(new Dictionary<int, int>(v));
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v.GetValueOrDefault(k - 1) < v.GetValueOrDefault(k + 1)))
                    x = v.GetValueOrDefault(k + 1);
                else
                    x = v.GetValueOrDefault(k - 1) + 1;

                var y = x - k;
                while (x < n && y < m && oldLines[x] == newLines[y]) { x++; y++; }
                v[k] = x;

                if (x >= n && y >= m)
                    return Backtrack(trace, oldLines, newLines, d);
            }
        }
        return null; // unreachable: d == max always finds the end
    }

    private static List<DiffLine> Backtrack(List<Dictionary<int, int>> trace, string[] a, string[] b, int finalD)
    {
        var result = new List<DiffLine>();
        int x = a.Length, y = b.Length;

        for (var d = finalD; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;
            var down = k == -d || (k != d && v.GetValueOrDefault(k - 1) < v.GetValueOrDefault(k + 1));
            var prevK = down ? k + 1 : k - 1;
            var prevX = v.GetValueOrDefault(prevK);
            var prevY = prevX - prevK;

            while (x > prevX && y > prevY)
            {
                result.Add(new(LineKind.Same, a[x - 1]));
                x--; y--;
            }
            // No edge before the very first step: d == 0 is only the initial matching snake.
            if (d > 0)
            {
                if (down)
                    result.Add(new(LineKind.Added, b[y - 1]));
                else
                    result.Add(new(LineKind.Removed, a[x - 1]));
            }
            x = prevX; y = prevY;
        }

        result.Reverse();
        return result;
    }
}
