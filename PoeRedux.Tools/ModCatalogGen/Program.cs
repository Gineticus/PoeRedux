using System.Globalization;
using System.Text;
using LibBundledGGPK3;
using PoeRedux.Data;
using PoeRedux.Models;
using PoeRedux.Patches.ModColors;

// Writes the list of mod lines that Color Mods offers for one game, read from that game's own files:
// every description line that a map (PoE 1) or waystone (PoE 2) prefix or suffix shows, plus the lines
// ModColorDefaults names for strongboxes, expeditions and altars. The game file is opened read-only.
//
//   dotnet run --project PoeRedux.Tools/ModCatalogGen -- <Content.ggpk or _.index.bin> <poe1|poe2> <output .cs>
if (args.Length != 3 || args[1] is not ("poe1" or "poe2"))
{
    Console.Error.WriteLine("usage: ModCatalogGen <Content.ggpk or _.index.bin> <poe1|poe2> <output .cs>");
    return 2;
}

var game = args[1] == "poe2" ? PoeGame.PoE2 : PoeGame.PoE1;
var layout = ModsLayout.For(game);

using var source = GameIndex.OpenReadOnly(args[0]);
var index = source.Index;
Console.WriteLine($"paths that failed to parse: {index.ParsePaths()}");

byte[] Read(string path) => index.TryGetFile(path, out var file)
    ? file.Read().ToArray()
    : throw new FileNotFoundException("Not in the game index: " + path);

var mods = new Dat64(Read(layout.TablePath("mods")));
var stats = new Dat64(Read(layout.TablePath("stats")));
var tags = new Dat64(Read(layout.TablePath("tags")));
var statIds = Enumerable.Range(0, stats.RowCount).Select(i => stats.String(i, 0)).ToArray();
var tagIds = Enumerable.Range(0, tags.RowCount).Select(i => tags.String(i, 0)).ToArray();
layout.Validate(mods, stats.RowCount);

// Stat description files, by lowercase path.
var csd = index.Files.Values
    .Where(f => f.Path is { } p && p.StartsWith(layout.StatDescriptionDir, StringComparison.Ordinal) && p.EndsWith(".csd", StringComparison.Ordinal))
    .ToDictionary(f => f.Path, f => StatDescriptionFile.Parse(Encoding.Unicode.GetString(f.Read().Span).TrimStart('﻿')), StringComparer.Ordinal);
Console.WriteLine($"{csd.Count} stat description files");

// The map file and the files it includes, in the order the game resolves them.
var chain = new List<StatDescriptionFile>();
void Follow(string path)
{
    path = path.ToLowerInvariant();
    if (!csd.TryGetValue(path, out var file) || chain.Contains(file))
        return;
    chain.Add(file);
    foreach (var include in file.Includes)
        Follow(include);
}
Follow(layout.StatDescriptionDir + "map_stat_descriptions.csd");

StatDescription? Describe(string stat, IEnumerable<StatDescriptionFile> files)
{
    foreach (var file in files)
    {
        if (file.Descriptions.FirstOrDefault(d => d.Stats.Contains(stat)) is { } d)
            return d;
        if (file.Hidden.Contains(stat))
            return null;
    }
    return null;
}

// Map or waystone affixes: the area domain, prefix or suffix. PoE 1 keeps spawn weights in the table, and a mod
// with no positive weight never rolls. PoE 2 stores the tags but no weights, so every affix is taken.
var lines = new Dictionary<string, Line>(StringComparer.Ordinal);
var affixes = 0;
for (var row = 0; row < mods.RowCount; row++)
{
    if (mods.Int32(row, ModsLayout.Domain) != layout.AreaDomain || mods.Int32(row, ModsLayout.GenerationType) is not (1 or 2))
        continue;
    var weights = mods.Int32Array(row, ModsLayout.SpawnWeightValues);
    if (layout.HasSpawnWeights && !weights.Any(w => w > 0))
        continue;
    affixes++;

    foreach (var (stat, min, max) in layout.Stats(mods, row))
    {
        var description = Describe(statIds[stat], chain);
        if (description is null)
            continue;
        var line = lines.TryGetValue(description.Key, out var existing) ? existing : lines[description.Key] = new Line(description, Category(description));
        line.Mods.Add(mods.String(row, 0));
        line.Sample(statIds[stat], min, max);
    }
}
Console.WriteLine($"{affixes} affixes, {lines.Count} description lines");

// Lines the defaults name that are not on map affixes: look them up in every file.
foreach (var entry in ModColorDefaults.For(game).Where(d => !lines.ContainsKey(d.Key)))
{
    var description = Describe(entry.Key, chain)
        ?? csd.Values.Select(f => f.Descriptions.FirstOrDefault(d => d.Key == entry.Key)).FirstOrDefault(d => d is not null);
    if (description is null)
        throw new InvalidDataException($"ModColorDefaults names {entry.Key}, which no description block in the game starts with.");
    var line = new Line(description, entry.Category ?? Category(description));
    lines[description.Key] = line;
    for (var row = 0; row < mods.RowCount; row++)
        foreach (var (stat, min, max) in layout.Stats(mods, row))
            if (statIds[stat] == entry.Key)
                line.Sample(entry.Key, min, max);
}

var missing = ModColorDefaults.For(game).Select(d => d.Key).Where(k => !lines.ContainsKey(k)).ToList();
if (missing.Count > 0)
    throw new InvalidDataException("Defaults without a line: " + string.Join(", ", missing));

WriteCatalog(args[2], game, lines.Values.OrderBy(l => CategoryOrder(l.Category)).ThenBy(l => l.Text, StringComparer.Ordinal).ToList());
Console.WriteLine($"wrote {args[2]}");
return 0;

static string Category(StatDescription d)
{
    var key = d.Key;
    var text = Line.Plain(d.English.FirstOrDefault()?.Text ?? "");
    if (key.Contains("drop", StringComparison.Ordinal) || key.Contains("quantity", StringComparison.Ordinal)
        || key.Contains("rarity", StringComparison.Ordinal) || key.Contains("pack_size", StringComparison.Ordinal)
        || key.Contains("experience", StringComparison.Ordinal) || key.Contains("number_of_", StringComparison.Ordinal)
        || key.Contains("potency", StringComparison.Ordinal) || key.Contains("vaal_vessel", StringComparison.Ordinal))
        return "Rewards";
    if (key.StartsWith("map_boss", StringComparison.Ordinal) || text.StartsWith("Unique Boss", StringComparison.Ordinal))
        return "Map boss";
    if (text.StartsWith("Player", StringComparison.Ordinal) || text.Contains(" Player ", StringComparison.Ordinal))
        return "Players";
    if (text.Contains("Monster", StringComparison.Ordinal) || text.StartsWith("Rare", StringComparison.Ordinal)
        || text.StartsWith("Magic", StringComparison.Ordinal))
        return "Monsters";
    return "Area";
}

static int CategoryOrder(string category) => category switch
{
    "Monsters" => 0, "Players" => 1, "Area" => 2, "Map boss" => 3, "Rewards" => 4,
    "Strongbox" => 5, "Expedition" => 6, "Altar" => 7, _ => 8,
};

static void WriteCatalog(string path, PoeGame game, List<Line> lines)
{
    var name = game == PoeGame.PoE2 ? "Poe2" : "Poe1";
    var sb = new StringBuilder();
    sb.AppendLine("// <auto-generated>");
    sb.AppendLine($"// Written by PoeRedux.Tools/ModCatalogGen from the {(game == PoeGame.PoE2 ? "PoE 2" : "PoE 1")} game files on {DateTime.Now:yyyy-MM-dd}.");
    sb.AppendLine("// Run it again after a game update instead of editing this file.");
    sb.AppendLine("// </auto-generated>");
    sb.AppendLine();
    sb.AppendLine("namespace PoeRedux.Patches.ModColors;");
    sb.AppendLine();
    sb.AppendLine("public static partial class ModLineCatalog");
    sb.AppendLine("{");
    sb.AppendLine($"    public static IReadOnlyList<ModLine> {name} {{ get; }} =");
    sb.AppendLine("    [");
    foreach (var line in lines)
        sb.AppendLine($"        new({Quote(line.Key)}, {Quote(line.Text)}, {Quote(line.SampleText())}, {Quote(line.Category)}),");
    sb.AppendLine("    ];");
    sb.AppendLine("}");
    File.WriteAllText(path, sb.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
}

static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

/// <summary>Column offsets of Mods.datc64 that this tool reads, checked against the table before use.</summary>
internal sealed record ModsLayout(PoeGame Game, string TableDir, string StatDescriptionDir, int AreaDomain, bool HasSpawnWeights,
    int StatsKey5, int Stat5Min, int StatsKey6, int Stat6Min)
{
    public const int Id = 0;
    public const int Domain = 94;
    public const int GenerationType = 106;
    public const int SpawnWeightTags = 158;
    public const int SpawnWeightValues = 174;
    private static readonly int[] StatsKeys = [30, 46, 62, 78];
    private const int Stat1Min = 126;

    public static ModsLayout For(PoeGame game) => game == PoeGame.PoE2
        ? new(game, "data/balance/", "data/statdescriptions/", 6, false, 302, 294, 406, 398)
        : new(game, "data/", "metadata/statdescriptions/", 5, true, 286, 278, 391, 383);

    public string TablePath(string table) => TableDir + table + ".datc64";

    public IEnumerable<(int Stat, int Min, int Max)> Stats(Dat64 mods, int row)
    {
        for (var i = 0; i < 4; i++)
            if (mods.ForeignRow(row, StatsKeys[i]) is { } stat)
                yield return (stat, mods.Int32(row, Stat1Min + i * 8), mods.Int32(row, Stat1Min + i * 8 + 4));
        if (mods.ForeignRow(row, StatsKey5) is { } s5)
            yield return (s5, mods.Int32(row, Stat5Min), mods.Int32(row, Stat5Min + 4));
        if (mods.ForeignRow(row, StatsKey6) is { } s6)
            yield return (s6, mods.Int32(row, Stat6Min), mods.Int32(row, Stat6Min + 4));
    }

    /// <summary>Stops before any output when the columns do not hold what this tool expects.</summary>
    public void Validate(Dat64 mods, int statRows)
    {
        if (mods.RowWidth < 420)
            throw new InvalidDataException($"Mods rows are {mods.RowWidth} bytes; the layout needs at least 420.");
        var areaAffixes = 0;
        for (var row = 0; row < mods.RowCount; row++)
        {
            var id = mods.String(row, Id);
            foreach (var column in StatsKeys.Append(StatsKey5).Append(StatsKey6))
                if (mods.ForeignRow(row, column) is { } stat && stat >= statRows)
                    throw new InvalidDataException($"{id}: stat key {stat} at column {column} is past the {statRows} stats.");
            var domain = mods.Int32(row, Domain);
            var generation = mods.Int32(row, GenerationType);
            if (domain is < 1 or > 100 || generation is < 1 or > 100)
                throw new InvalidDataException($"{id}: domain {domain} or generation type {generation} is out of range.");
            if (mods.Int32Array(row, SpawnWeightValues).Count is var values && HasSpawnWeights
                && values != mods.ForeignRowArray(row, SpawnWeightTags).Count)
                throw new InvalidDataException($"{id}: spawn weight tags and values differ in length.");
            if (domain == AreaDomain && generation is 1 or 2 && id.StartsWith("Map", StringComparison.Ordinal))
                areaAffixes++;
        }
        if (areaAffixes < 50)
            throw new InvalidDataException($"Only {areaAffixes} map affixes in domain {AreaDomain}; the domain number has probably changed.");
    }
}

internal sealed class Line(StatDescription description, string category)
{
    private readonly Dictionary<string, int> _samples = new(StringComparer.Ordinal);

    public string Key => description.Key;
    public string Category => category;
    public List<string> Mods { get; } = [];
    /// <summary>The same English variant as <see cref="SampleText"/>, with # for every number.</summary>
    public string Text
    {
        get
        {
            var text = Plain(Variant()?.Text ?? Key);
            for (var i = 0; i < description.Stats.Count; i++)
                text = text.Replace("{" + i + "}", "#").Replace("{" + i + ":+d}", "#").Replace("{" + i + ":d}", "#");
            return text;
        }
    }

    private List<int?> Values() => description.Stats.Select(s => _samples.TryGetValue(s, out var v) ? v : (int?)null).ToList();

    private StatText? Variant()
    {
        var values = Values();
        return description.English.FirstOrDefault(e => Matches(e, values)) ?? description.English.FirstOrDefault();
    }

    /// <summary>Keeps the value furthest from zero that a mod can roll for <paramref name="stat"/>.</summary>
    public void Sample(string stat, int min, int max)
    {
        var value = Math.Abs(max) >= Math.Abs(min) ? max : min;
        if (!_samples.TryGetValue(stat, out var current) || Math.Abs(value) > Math.Abs(current))
            _samples[stat] = value;
    }

    /// <summary>The English line with sample values, as a tooltip would show it.</summary>
    public string SampleText()
    {
        var values = Values();
        var variant = Variant();
        if (variant is null)
            return Key;
        var text = Plain(variant.Text);
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i] is { } v ? Shown(v, i + 1, variant.Handlers) : (double?)null;
            var plain = value is { } x ? x.ToString("0.##", CultureInfo.InvariantCulture) : "#";
            var signed = value is { } y ? (y >= 0 ? "+" : "") + y.ToString("0.##", CultureInfo.InvariantCulture) : "+#";
            text = text.Replace("{" + i + "}", plain).Replace("{" + i + ":+d}", signed).Replace("{" + i + ":d}", plain);
        }
        return text;
    }

    /// <summary>The value as the game prints it after the handlers for stat <paramref name="position"/> (1-based).</summary>
    private static double Shown(int value, int position, IReadOnlyList<string> handlers)
    {
        double shown = value;
        for (var i = 0; i + 1 < handlers.Count; i++)
        {
            if (handlers[i + 1] != position.ToString(CultureInfo.InvariantCulture))
                continue;
            shown = handlers[i] switch
            {
                "negate" => -shown,
                "divide_by_one_hundred" => shown / 100,
                "divide_by_ten_0dp" => Math.Round(shown / 10),
                "divide_by_two_0dp" => Math.Round(shown / 2),
                "per_minute_to_per_second" => shown / 60,
                "per_minute_to_per_second_0dp" => Math.Round(shown / 60),
                "milliseconds_to_seconds" => shown / 1000,
                "milliseconds_to_seconds_0dp" => Math.Round(shown / 1000),
                "deciseconds_to_seconds" => shown / 10,
                _ => shown,
            };
        }
        return shown;
    }

    private static bool Matches(StatText text, List<int?> values)
    {
        for (var i = 0; i < Math.Min(text.Ranges.Count, values.Count); i++)
        {
            if (values[i] is not { } v)
                continue;
            var parts = text.Ranges[i].Split('|');
            if (parts.Length == 1 && parts[0] != "#" && int.TryParse(parts[0], out var exact) && exact != v)
                return false;
            if (parts.Length == 2)
            {
                if (parts[0] != "#" && int.TryParse(parts[0], out var lo) && v < lo)
                    return false;
                if (parts[1] != "#" && int.TryParse(parts[1], out var hi) && v > hi)
                    return false;
            }
        }
        return true;
    }

    /// <summary>[Keyword|Shown text] becomes Shown text, [Keyword] becomes Keyword, \n becomes a space.</summary>
    public static string Plain(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '[' && text.IndexOf(']', i) is var close and > 0)
            {
                var inner = text[(i + 1)..close];
                var bar = inner.IndexOf('|');
                sb.Append(bar >= 0 ? inner[(bar + 1)..] : inner);
                i = close;
            }
            else
            {
                sb.Append(text[i]);
            }
        }
        return sb.ToString().Replace("\\n", " ");
    }
}

/// <summary>A game index opened for reading only, shared with anything else that has the file open.</summary>
internal sealed class GameIndex : IDisposable
{
    private readonly IDisposable _owner;

    private GameIndex(LibBundle3.Index index, IDisposable owner)
    {
        Index = index;
        _owner = owner;
    }

    public LibBundle3.Index Index { get; }

    public static GameIndex OpenReadOnly(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
        {
            var ggpk = new BundledGGPK(stream, false, false);
            return new GameIndex(ggpk.Index, ggpk);
        }
        // _.index.bin finds its bundles relative to the current directory.
        Environment.CurrentDirectory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var index = new LibBundle3.Index(stream, false, false);
        return new GameIndex(index, index);
    }

    public void Dispose() => _owner.Dispose();
}
