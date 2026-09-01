namespace PoeRedux.Services;

/// <summary>
/// The named groups a colored mod belongs to. A mod picks a group, the group carries the color,
/// so retinting every dangerous mod is one edit instead of sixty.
/// </summary>
public static class ModColorGroups
{
    public static readonly (string Name, byte R, byte G, byte B, string Tag)[] Defaults =
    [
        ("Dangerous", 209, 46, 46, "BAD"),
        ("Avoid", 255, 138, 61, "AVOID"),
        ("Annoying", 74, 230, 58, "MEH"),
        ("Neutral", 200, 200, 200, "-"),
        ("Good", 255, 204, 27, "GOOD"),
        ("Great", 14, 186, 255, "BEST"),
    ];

    public static Dictionary<string, string> DefaultTags() =>
        Defaults.ToDictionary(g => g.Name, g => g.Tag, StringComparer.Ordinal);

    public const string Fallback = "Dangerous";

    /// <summary>The five color names the patch shipped with, so older configs still load.</summary>
    private static readonly Dictionary<string, string> Legacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = "Dangerous",
        ["green"] = "Annoying",
        ["yellow"] = "Good",
        ["blue"] = "Great",
        ["pink"] = "Neutral",
    };

    public static IReadOnlyList<string> Names { get; } = Defaults.Select(g => g.Name).ToList();

    /// <summary>Maps a stored value, which may be a group, a legacy color name or a hex, to a group.</summary>
    public static string Normalize(string stored)
    {
        if (Names.Contains(stored, StringComparer.Ordinal))
            return stored;
        return Legacy.TryGetValue(stored, out var group) ? group : Fallback;
    }

    public static Dictionary<string, (byte R, byte G, byte B)> DefaultColors() =>
        Defaults.ToDictionary(g => g.Name, g => (g.R, g.G, g.B), StringComparer.Ordinal);
}
