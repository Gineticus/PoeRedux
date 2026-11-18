using LibBundledGGPK3;
using PoeRedux.Models;
using System.Text;

namespace PoeRedux.Services;

/// <summary>One line of a file comparison.</summary>
public enum DiffKind
{
    Same,
    Removed,
    Added,
    Gap,
}

public sealed record DiffLine(DiffKind Kind, string Text);

/// <summary>
/// Compares the files PoE Redux changed with their backed-up originals. The game file is opened for reading and
/// shared, so the game may run meanwhile; nothing is written.
/// </summary>
public sealed class ChangeReview : IDisposable
{
    private const int Context = 2;
    // Files whose line count changes are small in practice (.ao, .ot, .ui); larger searches are refused, since
    // the search keeps one row of positions per edit.
    private const int MaxEdits = 2_000;
    private const int MaxLinesToSearch = 60_000;

    private readonly BackupStore _store;
    private readonly IDisposable _owner;
    private readonly LibBundle3.Index _index;

    private ChangeReview(BackupStore store, LibBundle3.Index index, IDisposable owner)
    {
        _store = store;
        _index = index;
        _owner = owner;
    }

    /// <summary>Paths of the files that hold bytes PoE Redux wrote, sorted.</summary>
    public IReadOnlyList<string> Paths => _store.Entries.Where(e => e.Value.IsPatched).Select(e => e.Key).Order(StringComparer.Ordinal).ToList();

    public static ChangeReview Open(string gamePath, PoeGame game)
    {
        var store = BackupStore.Open(game);
        var stream = new FileStream(gamePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (gamePath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
        {
            var ggpk = new BundledGGPK(stream, false, false);
            return new ChangeReview(store, ggpk.Index, ggpk);
        }
        var index = new LibBundle3.Index(stream, false, false, new LibBundle3.DriveBundleFactory(Path.GetDirectoryName(Path.GetFullPath(gamePath))!));
        return new ChangeReview(store, index, index);
    }

    /// <summary>The changed lines of <paramref name="path"/> with a little context, or a note for files that are not text.</summary>
    public IReadOnlyList<DiffLine> Compare(string path)
    {
        var original = _store.ReadOriginal(path);
        if (!_index.TryGetFile(path, out var record))
            return [new DiffLine(DiffKind.Gap, "The game no longer has this file.")];
        var current = record.Read().ToArray();

        if (Decode(original) is not { } before || Decode(current) is not { } after)
            return [new DiffLine(DiffKind.Gap, $"Binary file: {original.Length:N0} bytes before, {current.Length:N0} bytes now.")];
        return Diff(Lines(before), Lines(after));
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

    /// <summary>UTF-16 with a byte order mark, or plain ASCII text such as shaders; null for anything else.</summary>
    internal static string? Decode(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length > 0 && bytes.All(b => b is 9 or 10 or 13 or (>= 32 and < 127)))
            return Encoding.ASCII.GetString(bytes);
        return null;
    }

    /// <summary>A line diff (Myers) cut down to the changed lines and <see cref="Context"/> lines around them.</summary>
    internal static List<DiffLine> Diff(string[] a, string[] b)
    {
        // Lines that only changed in place, the common case for these patches, need no search.
        var script = a.Length == b.Length ? InPlace(a, b) : Myers(a, b);
        if (script is null)
            return [new DiffLine(DiffKind.Gap, "Too many changes to show.")];

        var result = new List<DiffLine>();
        var keep = new bool[script.Count];
        for (var i = 0; i < script.Count; i++)
        {
            if (script[i].Kind == DiffKind.Same)
                continue;
            for (var k = Math.Max(0, i - Context); k <= Math.Min(script.Count - 1, i + Context); k++)
                keep[k] = true;
        }
        for (var i = 0; i < script.Count; i++)
        {
            if (keep[i])
                result.Add(script[i]);
            else if (i == 0 || keep[i - 1])
                result.Add(new DiffLine(DiffKind.Gap, "…"));
        }
        return result;
    }

    private static List<DiffLine> InPlace(string[] a, string[] b)
    {
        var script = new List<DiffLine>(a.Length);
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i])
            {
                script.Add(new DiffLine(DiffKind.Same, a[i]));
            }
            else
            {
                script.Add(new DiffLine(DiffKind.Removed, a[i]));
                script.Add(new DiffLine(DiffKind.Added, b[i]));
            }
        }
        return script;
    }

    // Myers' O((N+M)D) algorithm with the trace kept for backtracking; null when more than MaxEdits edits are needed.
    private static List<DiffLine>? Myers(string[] a, string[] b)
    {
        if (a.Length + b.Length > MaxLinesToSearch)
            return null;
        int n = a.Length, m = b.Length, max = n + m, offset = max;
        var v = new int[2 * max + 2];
        var trace = new List<int[]>();
        for (var d = 0; d <= Math.Min(max, MaxEdits); d++)
        {
            trace.Add((int[])v.Clone());
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[offset + k] = x;
                if (x >= n && y >= m)
                    return Backtrack(trace, a, b, offset, d);
            }
        }
        return null;
    }

    private static List<DiffLine> Backtrack(List<int[]> trace, string[] a, string[] b, int offset, int dEnd)
    {
        var script = new List<DiffLine>();
        int x = a.Length, y = b.Length;
        for (var d = dEnd; d > 0; d--)
        {
            var v = trace[d];
            var k = x - y;
            var prevK = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? k + 1 : k - 1;
            var prevX = v[offset + prevK];
            var prevY = prevX - prevK;
            while (x > prevX && y > prevY) { script.Add(new DiffLine(DiffKind.Same, a[x - 1])); x--; y--; }
            if (x == prevX) script.Add(new DiffLine(DiffKind.Added, b[y - 1]));
            else script.Add(new DiffLine(DiffKind.Removed, a[x - 1]));
            x = prevX;
            y = prevY;
        }
        while (x > 0 && y > 0) { script.Add(new DiffLine(DiffKind.Same, a[x - 1])); x--; y--; }
        script.Reverse();
        return script;
    }

    public void Dispose()
    {
        _store.CloseArchives();
        _owner.Dispose();
    }
}
