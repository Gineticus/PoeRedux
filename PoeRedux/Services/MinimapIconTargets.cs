using System.Text;
using System.Text.RegularExpressions;
using BundleIndex = LibBundle3.Index;

namespace PoeRedux.Services;

/// <summary>
/// Every object type that already draws a minimap icon, grouped by the icon it uses.
/// "All strongboxes" is the unit anyone actually wants to restyle, not one file at a time,
/// and the icon id is the only readable name available since index paths are lowercased.
/// </summary>
public static class MinimapIconTargets
{
    /// <summary>An icon the game ships, and every object type that uses it.</summary>
    public sealed record Target(string Icon, string Category, List<string> Paths)
    {
        public int FileCount => Paths.Count;
    }

    /// <summary>
    /// Matches a top level MinimapIcon block. The body holds only key = value lines, so stopping
    /// at the first closing brace is safe, and `\r?\n` is required because .ot files mix line
    /// endings within a single file. The brace itself is allowed on the same line as the header
    /// too: an empty block whose icon is chosen by script at runtime is written as `MinimapIcon
    /// {}` instead of on its own line.
    /// </summary>
    public static readonly Regex Block = new(
        @"(?<=(?:^|\r?\n))MinimapIcon[ \t]*(?:\r?\n[ \t]*)?\{(?<body>[^{}]*)\}",
        RegexOptions.Compiled);

    /// <summary>Marks a target with no static icon, keyed by its own object type so it stays distinct.</summary>
    private const string NoIconPrefix = "~NoIcon:";

    private static readonly Regex IconLine = new(
        @"(?<=(?:^|\r?\n))(?<indent>[ \t]*)icon[ \t]*=[ \t]*(?<value>[^\r\n]*)",
        RegexOptions.Compiled);

    public static Task<List<Target>> LoadAsync(string gameFilePath) =>
        Task.Run(() => Load(gameFilePath));

    public static List<Target> Load(string gameFilePath) => GameFile.Read(gameFilePath, Scan);

    public static List<Target> Scan(BundleIndex index)
    {
        var catalog = MinimapIconCatalog.Read(index);
        var byIcon = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in index.Files.Values)
        {
            if (!file.Path.EndsWith(".ot", StringComparison.OrdinalIgnoreCase))
                continue;

            var bytes = file.Read().Span;
            if (bytes.Length < 2 || bytes[0] != 0xFF || bytes[1] != 0xFE)
                continue;

            var text = Encoding.Unicode.GetString(bytes);
            if (!text.Contains("MinimapIcon", StringComparison.Ordinal))
                continue;

            var block = Block.Match(text);
            if (!block.Success)
                continue;

            // No icon key means the game picks one by script at runtime (chests are the common
            // case); still worth a row so a custom icon can be assigned, keyed by the object's
            // own file since these have no shared id to group under.
            var icon = IconOf(block.Groups["body"].Value, catalog) ?? NoIconPrefix + file.Path;

            if (!byIcon.TryGetValue(icon, out var paths))
                byIcon[icon] = paths = [];
            paths.Add(file.Path);
        }

        return byIcon
            .Select(kv => new Target(kv.Key, CategoryOf(kv.Key), kv.Value))
            .OrderBy(t => t.Category, StringComparer.Ordinal)
            .ThenBy(t => t.Icon, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The icon a block uses, resolving a bare row index through the catalog.</summary>
    public static string? IconOf(string body, List<string> catalog)
    {
        var match = IconLine.Match(body);
        if (!match.Success)
            return null;

        var value = match.Groups["value"].Value.Trim();
        if (value.StartsWith('"'))
            return value.Trim('"');
        return int.TryParse(value, out var row) && row >= 0 && row < catalog.Count
            ? catalog[row]
            : null;
    }

    /// <summary>Replaces the icon inside an existing block, leaving the file otherwise untouched.</summary>
    public static string Rewrite(string text, string newIcon)
    {
        // Substituting inside the matched text keeps the file's own line endings, which are
        // mixed within a single .ot, and leaves every other key in the block alone.
        return Block.Replace(
            text,
            block =>
            {
                if (IconLine.IsMatch(block.Groups["body"].Value))
                    return IconLine.Replace(
                        block.Value, m => $"{m.Groups["indent"].Value}icon = \"{newIcon}\"", 1);

                // No icon key yet, the block was empty (icon picked by script at runtime):
                // replace whatever sits between the braces, since it is only whitespace.
                var braceEnd = block.Value.IndexOf('{') + 1;
                var closingBrace = block.Value.LastIndexOf('}');
                return $"{block.Value[..braceEnd]}\r\n\ticon = \"{newIcon}\"\r\n{block.Value[closingBrace..]}";
            },
            1);
    }

    /// <summary>The label to show for a target's vanilla icon, in place of the internal key.</summary>
    public static string DisplayNameOf(string icon)
    {
        if (!icon.StartsWith(NoIconPrefix, StringComparison.Ordinal))
            return icon;
        var path = icon[NoIconPrefix.Length..];
        return path.EndsWith(".ot", StringComparison.Ordinal) ? path[..^3] : path;
    }

    private static readonly (string Prefix, string Category)[] Categories =
    [
        ("LootFilter", "Loot filter"),
        ("Expedition", "Expedition"),
        ("Heist", "Heist"),
        ("Delve", "Delve"),
        ("Bestiary", "Bestiary"),
        ("Harvest", "Harvest"),
        ("Incursion", "Incursion"),
        ("Labyrinth", "Labyrinth"),
        ("Sanctum", "Sanctum"),
        ("Blight", "Blight"),
        ("Breach", "Breach"),
        ("Abyss", "Abyss"),
        ("Deepwater", "Deepwater"),
        ("Metamorphosis", "Metamorph"),
        ("Delirium", "Delirium"),
        ("Hellscape", "Hellscape"),
        ("Warden", "Warden"),
        ("Betrayal", "Betrayal"),
        ("Ritual", "Ritual"),
        ("Sentinel", "Sentinel"),
        ("Ore", "Kalguur"),
        ("Village", "Kalguur"),
        ("Memory", "Atlas"),
        ("Map", "Atlas"),
        ("Reward", "Rewards"),
        ("Quest", "Quest"),
        ("Royale", "Royale"),
    ];

    public static string CategoryOf(string icon)
    {
        if (icon.StartsWith(NoIconPrefix, StringComparison.Ordinal))
            return "No Default Icon";

        foreach (var (prefix, category) in Categories)
            if (icon.StartsWith(prefix, StringComparison.Ordinal))
                return category;
        return "General";
    }
}
