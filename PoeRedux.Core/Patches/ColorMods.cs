using LibBundle3.Nodes;
using PoeRedux.Helpers;
using PoeRedux.Models;
using PoeRedux.Patches.ModColors;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

/// <summary>
/// Colors the text of chosen mod lines in the stat description files. Each line belongs to a group, and the
/// group's color is the one written.
/// </summary>
public class ColorMods : IPatch, IPatchSettings
{
    private static readonly Regex OurMarkup = new(@"<rgb\(\d+,\d+,\d+\)>\{\{(.*?)\}\}", RegexOptions.Compiled);

    private readonly string _directory;
    private readonly Dictionary<string, ModColorDefault> _defaults;
    private int _modsFound;

    public ColorMods(PoeGame game)
    {
        Game = game;
        _directory = game == PoeGame.PoE2 ? "data/statdescriptions" : "metadata/statdescriptions";
        Lines = ModLineCatalog.For(game);
        _defaults = ModColorDefaults.For(game).ToDictionary(d => d.Key, StringComparer.Ordinal);
        ResetSettings();
    }

    public string Id => "color-mods";
    public string Name => "Color Mods";
    public string Description => "Colors map mods by group";
    public PatchGroup Group => PatchGroup.Readability;

    public PoeGame Game { get; }

    /// <summary>The mod lines this game has, read from its files when PoE Redux was built.</summary>
    public IReadOnlyList<ModLine> Lines { get; }

    public List<ModColorGroup> Groups { get; } = ModColorGroup.CreateDefaults();

    /// <summary>Choice for every line in <see cref="Lines"/>, by key.</summary>
    public Dictionary<string, ModLineChoice> Choices { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Description blocks of chosen lines that the last apply found in the game files.
    /// Zero means the game no longer has these mods where this patch looks for them.
    /// </summary>
    public int ModsFound => _modsFound;

    public ModLineChoice DefaultChoice(string key) =>
        _defaults.TryGetValue(key, out var d) ? new ModLineChoice(true, d.Group) : new ModLineChoice(false, ModColorDefaults.Neutral);

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        _modsFound = 0;
        var colors = Groups.ToDictionary(g => g.Id, g => g.Color.Markup, StringComparer.Ordinal);
        var chosen = Choices
            .Where(c => colors.ContainsKey(c.Value.Group))
            .ToDictionary(c => c.Key, c => c.Value.Enabled ? colors[c.Value.Group] : null, StringComparer.Ordinal);
        if (!chosen.Values.Any(c => c is not null))
            return;

        var transform = Utf16.Edit(text => Recolor(text, chosen));
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, _directory), ".csd"))
            plan.Add(file, transform);
    }

    private enum ReadState
    {
        SeekingDescription,
        ReadingStatName,
        ReadingLineCount,
        Recoloring,
    }

    // A "description" line starts a block. The next line lists its stats, and the first one decides the color.
    // Then, for each language, a line with a count n is followed by n lines of text, which get the color.
    // A null color means the line is chosen but off: color written by an earlier apply is taken off.
    private string Recolor(string text, Dictionary<string, string?> colors)
    {
        string[] lines = text.Split("\r\n");
        var state = ReadState.SeekingDescription;
        string? color = null;
        int linesLeft = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line.StartsWith("description"))
            {
                state = ReadState.ReadingStatName;
                continue;
            }

            switch (state)
            {
                case ReadState.ReadingStatName:
                    string[] words = line.Split(' ');
                    if (words.Length >= 2 && colors.TryGetValue(words[1], out color))
                    {
                        if (color is not null)
                            Interlocked.Increment(ref _modsFound); // files are patched in parallel
                        state = ReadState.ReadingLineCount;
                    }
                    else
                    {
                        state = ReadState.SeekingDescription;
                    }
                    break;

                case ReadState.ReadingLineCount:
                    // Lines that are not a number, such as lang "Russian", are skipped.
                    if (int.TryParse(line.Replace("\t", "").Split(' ')[0], out int count))
                    {
                        linesLeft = count;
                        state = count > 0 ? ReadState.Recoloring : ReadState.ReadingLineCount;
                    }
                    break;

                case ReadState.Recoloring:
                    lines[i] = RecolorLine(line, color);
                    if (--linesLeft == 0)
                        state = ReadState.ReadingLineCount;
                    break;
            }
        }

        return string.Join("\r\n", lines);
    }

    // With a color: "text" becomes "<color>{{text}}", and an existing <...> style is swapped for the color.
    // Without one: <rgb(...)>{{text}} becomes text again. The game's own stat descriptions use named styles
    // such as <enchanted>, never rgb, so only markup this patch wrote is taken off.
    private static string RecolorLine(string line, string? color)
    {
        if (color is null)
            return OurMarkup.Replace(line, m => m.Groups[1].Value);

        if (line.Contains('<'))
            return Regex.Replace(line, "<.*?>", _ => $"<{color}>");

        return Regex.Replace(line, "\".*?\"", m => $"\"<{color}>{{{{{m.Value.Replace("\"", "")}}}}}\"");
    }

    public JsonObject? SaveSettings()
    {
        var groups = new JsonObject();
        foreach (var group in Groups.Where(g => g.Color != g.DefaultColor))
            groups[group.Id] = group.Color.Hex;

        var lines = new JsonObject();
        foreach (var (key, choice) in Choices.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (choice != DefaultChoice(key))
                lines[key] = new JsonObject { ["on"] = choice.Enabled, ["group"] = choice.Group };
        }

        if (groups.Count == 0 && lines.Count == 0)
            return null;
        var settings = new JsonObject();
        if (groups.Count > 0)
            settings["groups"] = groups;
        if (lines.Count > 0)
            settings["lines"] = lines;
        return settings;
    }

    public void LoadSettings(JsonObject settings)
    {
        ResetSettings();
        if (settings["groups"] is JsonObject groups)
        {
            foreach (var group in Groups)
            {
                if (groups[group.Id] is JsonValue v && v.TryGetValue<string>(out var hex) && Rgb.TryParse(hex, out var rgb))
                    group.Color = rgb;
            }
        }
        if (settings["lines"] is JsonObject lines)
        {
            var groupIds = Groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var (key, node) in lines)
            {
                // A line the game no longer has is dropped with its saved choice.
                if (!Choices.TryGetValue(key, out var current) || node is not JsonObject entry)
                    continue;
                var enabled = entry["on"] is JsonValue on && on.TryGetValue<bool>(out var b) ? b : current.Enabled;
                var group = entry["group"] is JsonValue g && g.TryGetValue<string>(out var id) && groupIds.Contains(id) ? id : current.Group;
                Choices[key] = new ModLineChoice(enabled, group);
            }
        }
    }

    public void ResetSettings()
    {
        foreach (var group in Groups)
            group.Color = group.DefaultColor;
        Choices.Clear();
        foreach (var line in Lines)
            Choices[line.Key] = DefaultChoice(line.Key);
    }
}
