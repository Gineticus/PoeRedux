using System.Windows;
using System.Windows.Media;

namespace PoeRedux.Services;

/// <summary>
/// Draws a minimap icon from its id. The 396 loot filter ids name their own size, color and
/// shape, and the game draws them as flat shapes, so a vector glyph is the same picture rather
/// than an approximation. Named icons have no addressable art, so they get a neutral chip.
/// </summary>
public static class IconGlyphs
{
    private static readonly Dictionary<string, Color> Palette = new(StringComparer.Ordinal)
    {
        ["Blue"] = Color.FromRgb(0x3B, 0x82, 0xF6),
        ["Brown"] = Color.FromRgb(0x8B, 0x5A, 0x2B),
        ["Cyan"] = Color.FromRgb(0x22, 0xD3, 0xEE),
        ["Green"] = Color.FromRgb(0x22, 0xC5, 0x5E),
        ["Grey"] = Color.FromRgb(0x9C, 0xA3, 0xAF),
        ["Orange"] = Color.FromRgb(0xF9, 0x73, 0x16),
        ["Pink"] = Color.FromRgb(0xEC, 0x48, 0x99),
        ["Purple"] = Color.FromRgb(0xA8, 0x55, 0xF7),
        ["Red"] = Color.FromRgb(0xEF, 0x44, 0x44),
        ["White"] = Color.FromRgb(0xF8, 0xFA, 0xFC),
        ["Yellow"] = Color.FromRgb(0xEA, 0xB3, 0x08),
    };

    // All drawn inside a 100x100 box so one scale factor covers every shape.
    private static readonly Dictionary<string, string> Outlines = new(StringComparer.Ordinal)
    {
        ["Circle"] = "M 5,50 A 45,45 0 1 0 95,50 A 45,45 0 1 0 5,50 Z",
        ["Square"] = "M 10,10 H 90 V 90 H 10 Z",
        ["Diamond"] = "M 50,4 L 96,50 L 50,96 L 4,50 Z",
        ["Triangle"] = "M 50,6 L 95,88 L 5,88 Z",
        ["Hexagon"] = "M 50,4 L 91,27 L 91,73 L 50,96 L 9,73 L 9,27 Z",
        ["Pentagon"] = "M 50,4 L 95,38 L 78,92 L 22,92 L 5,38 Z",
        ["Star"] = "M 50,3 L 62,38 L 98,38 L 69,60 L 80,95 L 50,73 L 20,95 L 31,60 L 2,38 L 38,38 Z",
        ["Cross"] = "M 36,6 H 64 V 36 H 94 V 64 H 64 V 94 H 36 V 64 H 6 V 36 H 36 Z",
        ["Moon"] = "M 64,5 A 45,45 0 1 0 64,95 A 34,34 0 1 1 64,5 Z",
        ["Raindrop"] = "M 50,4 C 74,38 88,54 88,68 A 38,38 0 1 1 12,68 C 12,54 26,38 50,4 Z",
        ["Kite"] = "M 50,3 L 88,36 L 50,97 L 12,36 Z",
        ["UpsideDownHouse"] = "M 5,8 H 95 V 58 L 50,96 L 5,58 Z",
    };

    private static readonly Brush Neutral = Frozen(new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)));
    private static readonly Geometry Chip = Frozen(Geometry.Parse("M 14,14 H 86 V 86 H 14 Z"));

    /// <summary>Shape names in a fixed order, for the picker's filter.</summary>
    public static IReadOnlyList<string> Shapes { get; } = MinimapIconCatalog.Shapes;

    public static IReadOnlyList<string> Colors { get; } = MinimapIconCatalog.Colors;

    public static Geometry GeometryFor(string icon) =>
        MinimapIconCatalog.TryDescribe(icon, out _, out _, out var shape)
            && Outlines.TryGetValue(shape, out var outline)
            ? Frozen(Geometry.Parse(outline))
            : Chip;

    public static Brush BrushFor(string icon) =>
        MinimapIconCatalog.TryDescribe(icon, out _, out var color, out _)
            && Palette.TryGetValue(color, out var value)
            ? Frozen(new SolidColorBrush(value))
            : Neutral;

    /// <summary>How much of the 20px cell the glyph fills, following the id's own size word.</summary>
    public static double ScaleFor(string icon) =>
        MinimapIconCatalog.TryDescribe(icon, out var size, out _, out _)
            ? size switch { "Large" => 1.0, "Medium" => 0.75, _ => 0.55 }
            : 0.7;

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        // Frozen brushes and geometries are shared safely across the virtualized lists.
        freezable.Freeze();
        return freezable;
    }
}
