using System.IO;
using System.Text;

using LibBundle3.Nodes;

using PoeRedux.Services;

namespace PoeRedux.Patches;

/// <summary>
/// Highlights strong item mod rolls. The color says how good the roll is, as a fraction of the
/// stat's craftable ceiling, so the same color means the same quality on every stat. Not the
/// client's tier number: that depends on the item base, and the base never reaches the
/// description file.
/// </summary>
public class ModTierColors : IPatch
{
    public string Name => "Mod Tier Colors";
    public object Description => "Highlights strong mod rolls on hover, one color per quality band.";

    /// <summary>Stat ids the Color Mods patch owns, which win over this one.</summary>
    public HashSet<string> ExcludedStatIds { get; set; } = new(StringComparer.Ordinal);

    /// <summary>When non null, only these stat ids are highlighted.</summary>
    public HashSet<string>? EnabledStatIds { get; set; }

    /// <summary>The quality bands, each a fraction of the stat's ceiling and a color.</summary>
    public List<RollTier> Tiers { get; set; } = [.. RollTiers.Defaults];

    /// <summary>
    /// Colors a short band label in front of the mod instead of the whole line, so the mod text
    /// stays readable.
    /// </summary>
    public bool TagPrefix { get; set; }

    /// <summary>
    /// Measure against every mod ladder for the stat, so a maxed one-hand roll reads as perfect
    /// even though a two-hander reaches higher. Off compares against the single hardest roll,
    /// which under-reports anything on a smaller base.
    /// </summary>
    public bool PerLadder { get; set; } = true;

    /// <summary>
    /// Smallest share of the stat.s ceiling a ladder must reach before its top counts as perfect.
    /// Without it a weak roll of a long ladder borrows the verdict of a short ladder it happens to
    /// overlap: 37 life is tier 7 of the real ladder but the top of the hybrid armour and life one.
    /// </summary>
    public double MinLadderShare { get; set; } = 0.5;

    /// <summary>
    /// Shortens the " (Tier: {0})" / " (Tier: {0}, Lvl: {1})" suffix the client appends after a
    /// mod's own line. Lives here rather than on a client-strings patch because tier presentation
    /// is this patch's whole job.
    /// </summary>
    public bool CompactTier { get; set; }

    private static readonly Dictionary<string, string> CompactTierReplacements = new(StringComparer.Ordinal)
    {
        ["ModDescriptionLineTier"] = " T{0}",
        ["ModDescriptionLineTierLevel"] = " T{0} L{1}",
    };

    public void Apply(DirectoryNode root)
    {
        var data = NavigateTo(root, "data")
            ?? throw new DirectoryNotFoundException("data directory not found in the game files");
        var scales = ModTierBands.Build(
            ReadFile(data, "mods.datc64"),
            ReadFile(data, "stats.datc64"),
            out _);

        if (CompactTier)
        {
            var clientStrings = data.Children.OfType<FileNode>()
                .FirstOrDefault(f => f.Name == "clientstrings.datc64");
            if (clientStrings is null)
                PatchReport.FileMissing(ClientStringsTable.Path);
            else if (ClientStringsTable.Rewrite(clientStrings,
                (id, _) => CompactTierReplacements.TryGetValue(id, out var r) ? r : null) == 0)
                PatchReport.NoMatch(ClientStringsTable.Path, "the tier description line ids");
        }

        var descriptions = NavigateTo(root, "metadata", "statdescriptions")
            ?? throw new DirectoryNotFoundException("statdescriptions directory not found");

        var tiers = RollTiers.Ordered(Tiers);
        var colored = 0;
        var files = 0;
        foreach (var node in Recurse(descriptions))
        {
            if (!node.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                continue;

            var record = node.Record;
            // Decoding UTF-16 bytes that start with FF FE yields a leading U+FEFF character.
            // It has to come off here, or writing the preamble back produces a second BOM and
            // the client refuses to parse the file.
            var text = Encoding.Unicode.GetString(record.Read().Span).TrimStart('﻿');
            var lines = text.Split("\r\n").ToList();
            if (!Rewrite(lines, scales, ExcludedStatIds, EnabledStatIds, tiers, TagPrefix, PerLadder, MinLadderShare, ref colored))
                continue;

            var newBytes = Encoding.Unicode.GetPreamble()
                .Concat(Encoding.Unicode.GetBytes(string.Join("\r\n", lines)))
                .ToArray();
            BackupManager.RecordOriginal(record);
            record.Write(newBytes);
            files++;
        }

        // No bands or an empty stat selection is a legitimate no-op; a real selection that
        // colored nothing means the description shape or the mod tables moved.
        var configured = tiers.Count > 0 && EnabledStatIds?.Count != 0;
        if (files == 0 && configured)
            PatchReport.NoMatch("metadata/statdescriptions", "a roll it could grade");
    }

    public static bool Rewrite(
        List<string> lines,
        Dictionary<string, StatScale> scales,
        HashSet<string> excluded,
        HashSet<string>? enabledStatIds,
        List<RollTier> tiers,
        bool tagPrefix,
        bool perLadder,
        double minLadderShare,
        ref int colored)
    {
        if (tiers.Count == 0)
            return false;

        var changed = false;

        for (var i = 0; i < lines.Count - 2; i++)
        {
            if (!lines[i].StartsWith("description", StringComparison.Ordinal))
                continue;

            var header = lines[i + 1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // A description covering several stats has no single value to measure.
            if (header.Length != 2 || header[0] != "1")
                continue;
            var statId = header[1];
            if (excluded.Contains(statId) || !scales.TryGetValue(statId, out var scale))
                continue;
            if (enabledStatIds is not null && !enabledStatIds.Contains(statId))
                continue;

            if (!int.TryParse(lines[i + 2].Trim(), out var rangeCount)
                || rangeCount < 1
                || i + 2 + rangeCount >= lines.Count)
                continue;

            var originals = new List<ValueRange>(rangeCount);
            var parsed = true;
            for (var r = 1; r <= rangeCount; r++)
            {
                if (ValueRange.TryParse(lines[i + 2 + r], out var range))
                    originals.Add(range);
                else
                {
                    parsed = false;
                    break;
                }
            }
            if (!parsed || originals.Any(o => o.Body.Contains("<rgb(", StringComparison.Ordinal)))
                continue; // unrecognised shape, or already highlighted

            var plan = Plan(scale, tiers, perLadder, minLadderShare);
            var replacement = new List<string>();
            var touched = false;
            foreach (var original in originals)
                touched |= Split(original, plan, tagPrefix, replacement);
            if (!touched)
                continue;

            lines[i + 2] = $"\t{replacement.Count}";
            lines.RemoveRange(i + 3, rangeCount);
            lines.InsertRange(i + 3, replacement);

            colored++;
            changed = true;
            i += replacement.Count + 2;
        }

        return changed;
    }

    /// <summary>
    /// Which band each value belongs to, as ordered, non overlapping segments. A value counts as
    /// near perfect when it is near the top of any ladder that can reach it, so a maxed one-hand
    /// roll and a maxed two-hand roll both read as perfect even though their numbers differ.
    /// </summary>
    private static List<(int Lo, int? Hi, RollTier? Tier)> Plan(
        StatScale scale,
        List<RollTier> tiers,
        bool perLadder,
        double minLadderShare)
    {
        var ladders = perLadder
            ? scale.FamilyMaxima.Where(m => m >= minLadderShare * scale.Max).DefaultIfEmpty(scale.Max).ToArray()
            : [scale.Max];
        // 100% and above both mean "past the ceiling", so a config that was clamped to 100 by
        // the old bug still behaves as intended.
        var graded = tiers.Where(t => t.MinFraction < 1.0).ToList();
        var ultra = tiers.FirstOrDefault(t => t.MinFraction >= 1.0);

        var bounds = new SortedSet<int> { 1 };
        foreach (var familyMax in ladders)
        {
            foreach (var tier in graded)
                bounds.Add(Math.Max(1, (int)Math.Ceiling(tier.MinFraction * familyMax)));
            bounds.Add(familyMax + 1);
        }
        if (ultra is not null)
            bounds.Add(scale.Max + 1);

        var edges = bounds.ToList();
        var segments = new List<(int Lo, int? Hi, RollTier? Tier)>();
        for (var i = 0; i < edges.Count; i++)
        {
            var lo = edges[i];
            int? hi = i + 1 < edges.Count ? edges[i + 1] - 1 : null;
            var tier = BandFor(lo, scale, ladders, graded, ultra);
            // Merge with the previous segment when the band did not change.
            if (segments.Count > 0 && segments[^1].Tier == tier)
                segments[^1] = (segments[^1].Lo, hi, tier);
            else
                segments.Add((lo, hi, tier));
        }
        return segments;
    }

    private static RollTier? BandFor(
        int value,
        StatScale scale,
        int[] ladders,
        List<RollTier> graded,
        RollTier? ultra)
    {
        if (value > scale.Max)
            return ultra;
        // Bands are ordered best first, so the first ladder that qualifies wins.
        foreach (var tier in graded)
            foreach (var familyMax in ladders)
                if (value <= familyMax && value >= (int)Math.Ceiling(tier.MinFraction * familyMax))
                    return tier;
        return null;
    }

    /// <summary>
    /// Rewrites one of a description's value ranges as one line per band it spans, keeping that
    /// range's own wording. A range no band reaches, a negative one for instance, comes back
    /// unchanged. Returns whether anything was highlighted.
    /// </summary>
    private static bool Split(
        ValueRange original,
        List<(int Lo, int? Hi, RollTier? Tier)> plan,
        bool tagPrefix,
        List<string> output)
    {
        var highlighted = false;
        var emitted = 0;
        // Descending, so the best roll reads first in the file.
        for (var i = plan.Count - 1; i >= 0; i--)
        {
            var (lo, hi, tier) = plan[i];
            var clampedLo = Math.Max(lo, original.Lo ?? int.MinValue);
            var clampedHi = Min(hi, original.Hi);
            if (clampedHi.HasValue && clampedLo > clampedHi.Value)
                continue;

            // Keep the range's own open ends rather than inventing bounds it never had.
            int? outLo = original.Lo is null && lo == plan[0].Lo ? null : clampedLo;
            var body = tier is null ? original.Body : Style(original.Body, tier, tagPrefix);
            output.Add(ValueRange.Format(outLo, clampedHi, body, original.Trailer));
            emitted++;
            if (tier is not null)
                highlighted = true;
        }

        if (emitted == 0)
            output.Add(ValueRange.Format(original.Lo, original.Hi, original.Body, original.Trailer));
        return highlighted;
    }

    private static int? Min(int? a, int? b) => (a, b) switch
    {
        (null, null) => null,
        (null, not null) => b,
        (not null, null) => a,
        _ => Math.Min(a.Value, b.Value),
    };

    private static string Style(string body, RollTier tier, bool tagPrefix)
    {
        var color = $"rgb({tier.R},{tier.G},{tier.B})";
        return tagPrefix
            // The body needs a run of its own: unstyled text after one inherits its color.
            ? $"<{color}>{{{{{tier.Tag}}}}} <{ModTextStyler.BodyAnnotation}>{{{{{body}}}}}"
            : $"<{color}>{{{{{body}}}}}";
    }

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        var current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    private static byte[] ReadFile(DirectoryNode dir, string name)
    {
        var node = dir.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == name)
            ?? throw new FileNotFoundException("not found in the game files: " + name);
        return node.Record.Read().ToArray();
    }

    private static IEnumerable<FileNode> Recurse(DirectoryNode dir)
    {
        foreach (var child in dir.Children)
        {
            if (child is FileNode file)
                yield return file;
            else if (child is DirectoryNode sub)
                foreach (var nested in Recurse(sub))
                    yield return nested;
        }
    }

}
