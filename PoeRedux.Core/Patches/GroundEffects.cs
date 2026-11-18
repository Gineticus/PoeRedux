using LibBundle3.Nodes;
using LibBundle3.Records;
using PoeRedux.Data;
using PoeRedux.Helpers;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

/// <summary>
/// The files the game draws ground effects with, such as burning, chilled or caustic ground. The ground effects
/// table names .ao files; those extend other .ao files, attach objects and start particles, trails and effect
/// packs, and every such reference is followed. Particle and trail files name no further effect files (checked
/// in both games on 18 Nov 2026), so they end the search.
/// </summary>
internal static partial class GroundEffects
{
    // PoE 1 keeps the table in data, PoE 2 in data/balance.
    private static readonly string[] Tables = ["data/groundeffects.datc64", "data/balance/groundeffects.datc64"];

    // A quoted game path to a file that can draw something: "Metadata/Particles/ground_effects/fire.pet".
    [GeneratedRegex(@"""(metadata/[^""]+?\.(?:ao|aoc|epk|pet|trl))""", RegexOptions.IgnoreCase)]
    private static partial Regex QuotedPath();

    [GeneratedRegex(@"^extends\s+""(metadata/[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Extends();

    /// <summary>Lowercase paths of the files, worked out once per apply.</summary>
    public static IReadOnlySet<string> Files(DirectoryNode root, PatchPlan plan) =>
        plan.Shared(nameof(GroundEffects), () => Find(root, plan));

    private static HashSet<string> Find(DirectoryNode root, PatchPlan plan)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (Tables.Select(t => GameTree.FindFile(root, t)).FirstOrDefault(f => f is not null) is not { } table)
            return found;

        var toRead = new List<FileRecord>();
        void Visit(string path)
        {
            path = path.ToLowerInvariant();
            if (!found.Add(path))
                return;
            if (path.EndsWith(".ao", StringComparison.Ordinal))
                Visit(path + "c");
            // Particles and trails are leaves; the other files are read for what they use.
            if (!path.EndsWith(".pet", StringComparison.Ordinal) && !path.EndsWith(".trl", StringComparison.Ordinal)
                && GameTree.FindFile(root, path) is { } file)
                toRead.Add(file.Record);
        }

        foreach (var path in AoPaths(plan.ReadOriginals([table.Record])[table.Record]))
            Visit(path);
        while (toRead.Count > 0)
        {
            var batch = toRead.ToList();
            toRead.Clear();
            foreach (var bytes in plan.ReadOriginals(batch).Values)
            {
                foreach (var path in References(Text(bytes)))
                    Visit(path);
            }
        }
        return found;
    }

    /// <summary>The .ao paths in the strings of a ground effects table, whatever columns hold them.</summary>
    internal static IEnumerable<string> AoPaths(byte[] table)
    {
        var data = new Dat64(table).DataOffset;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        // Strings are UTF-16 and usually start at even offsets, but nothing requires it.
        for (var start = data; start < data + 2; start++)
        {
            var run = new StringBuilder();
            for (var i = start; i + 1 < table.Length; i += 2)
            {
                var c = (char)(table[i] | table[i + 1] << 8);
                if (c is >= ' ' and <= '~')
                {
                    run.Append(c);
                    continue;
                }
                Add(run.ToString());
                run.Clear();
            }
            Add(run.ToString());
        }
        return paths;

        void Add(string text)
        {
            var at = text.IndexOf("metadata/", StringComparison.OrdinalIgnoreCase);
            if (at >= 0 && text.EndsWith(".ao", StringComparison.OrdinalIgnoreCase))
                paths.Add(text[at..].ToLowerInvariant());
        }
    }

    /// <summary>The files an .ao, .aoc or .epk file uses: quoted paths, and the .ao and .aoc it extends.</summary>
    internal static IEnumerable<string> References(string text)
    {
        foreach (Match m in QuotedPath().Matches(text))
            yield return m.Groups[1].Value;
        foreach (Match m in Extends().Matches(text))
        {
            yield return m.Groups[1].Value + ".ao";
            yield return m.Groups[1].Value + ".aoc";
        }
    }

    private static string Text(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
            ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
            : Encoding.Unicode.GetString(bytes);
}
