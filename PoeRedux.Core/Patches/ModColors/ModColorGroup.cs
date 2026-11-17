using System.Globalization;

namespace PoeRedux.Patches.ModColors;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>The color as the game's text markup writes it: rgb(209,46,46).</summary>
    public string Markup => $"rgb({R},{G},{B})";

    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Parses "#RRGGBB" or "RRGGBB".</summary>
    public static bool TryParse(string? text, out Rgb rgb)
    {
        rgb = default;
        var hex = text?.Trim().TrimStart('#') ?? "";
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;
        rgb = new Rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }
}

/// <summary>
/// A named color that mod lines are assigned to. Changing the group's color recolors every line in it.
/// </summary>
public sealed class ModColorGroup(string id, Rgb defaultColor)
{
    public string Id { get; } = id;
    public Rgb DefaultColor { get; } = defaultColor;
    public Rgb Color { get; set; } = defaultColor;

    /// <summary>
    /// The five groups. Their default colors are the five colors PoE Redux used before groups existed,
    /// so a default apply writes the same text as before.
    /// </summary>
    public static List<ModColorGroup> CreateDefaults() =>
    [
        new(ModColorDefaults.Dangerous, new Rgb(209, 46, 46)),
        new(ModColorDefaults.Annoying, new Rgb(74, 230, 58)),
        new(ModColorDefaults.Good, new Rgb(255, 204, 27)),
        new(ModColorDefaults.Great, new Rgb(14, 186, 255)),
        new(ModColorDefaults.Neutral, new Rgb(227, 158, 255)),
    ];
}

/// <summary>Whether a mod line is recolored, and with which group's color.</summary>
public readonly record struct ModLineChoice(bool Enabled, string Group);
