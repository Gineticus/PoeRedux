using LibBundle3.Nodes;
using PoeRedux.Helpers;
using PoeRedux.Patches.ModColors;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

/// <summary>
/// Reveals the minimap and sets its colors by editing two shaders. Each edit matches a whole statement
/// rather than the shipped numbers, so it also matches a file that an earlier apply already changed.
/// </summary>
public class Minimap : IPatch, IPatchSettings
{
    public const double DefaultReveal = 0.18;
    public static readonly Rgb DefaultWalkableColor = new(0, 0, 0);
    public const double DefaultWalkableOpacity = 0.3;
    public static readonly Rgb DefaultEdgeColor = new(255, 255, 255);
    public const double DefaultEdgeOpacity = 0.1;

    // The shader works in linear HDR. The edge color it ships with is far above 1, and that overbright is what
    // makes walls stand out from the fill, so the chosen edge color is multiplied by this.
    private const double EdgeGain = 12.0;

    private const string FullyRevealed = "res_color = float4(1.0f, 0.0f, 0.0f, 1.0f);";
    private static readonly Regex FloorLine = new(@"[ \t]*res_color = max\(res_color, [0-9.]+f\);", RegexOptions.Compiled);
    private static readonly Regex WalkableColorStatement = new(@"float4 walkable_color = float4\([^;]*\);", RegexOptions.Compiled);
    private static readonly Regex EdgeLerpStatement =
        new(@"float4 walkability_map_color = lerp\(walkable_color, float4\([^)]*\), walkable_to_edge_ratio\);", RegexOptions.Compiled);

    public string Id => "minimap";
    public string Name => "Minimap";
    public string Description => "Shows the whole minimap from the start and sets its colors.";
    public PatchGroup Group => PatchGroup.MapAndCamera;

    /// <summary>
    /// How strongly unexplored parts of the map show, from 0 (as the game draws them) to 1 (as if explored).
    /// </summary>
    public double Reveal { get; set; } = DefaultReveal;

    /// <summary>Fill of the walkable area.</summary>
    public Rgb WalkableColor { get; set; } = DefaultWalkableColor;
    public double WalkableOpacity { get; set; } = DefaultWalkableOpacity;

    /// <summary>Color the fill turns into at walls and edges.</summary>
    public Rgb EdgeColor { get; set; } = DefaultEdgeColor;
    public double EdgeOpacity { get; set; } = DefaultEdgeOpacity;

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var reveal = Number(Math.Clamp(Reveal, 0, 1));
        var fill = Color(WalkableColor, 1.0, WalkableOpacity);
        var edge = Color(EdgeColor, EdgeGain, EdgeOpacity);

        if (GameTree.FindFile(root, "shaders/minimap_visibility_pixel.hlsl") is { } visibility)
            plan.Add(visibility, Ascii(text => RevealFloor(text, reveal)));

        if (GameTree.FindFile(root, "shaders/minimap_blending_pixel.hlsl") is { } blending)
            plan.Add(blending, Ascii(text => Recolor(text, fill, edge)));
    }

    /// <summary>
    /// Adds a floor to the visibility value right after the fully revealed assignment. That line sits in an if
    /// without braces, so the floor is outside the if and applies to every pixel.
    /// </summary>
    internal static string? RevealFloor(string text, string reveal)
    {
        var line = $"\tres_color = max(res_color, {reveal}f);";
        if (FloorLine.Match(text) is { Success: true } existing)
            return text.Replace(existing.Value, line, StringComparison.Ordinal);

        var lines = text.Split("\r\n").ToList();
        var index = lines.FindIndex(l => l.Contains(FullyRevealed, StringComparison.Ordinal));
        if (index < 0)
            return null;
        lines.Insert(index + 1, line);
        return string.Join("\r\n", lines);
    }

    internal static string? Recolor(string text, string fill, string edge)
    {
        if (!WalkableColorStatement.IsMatch(text) || !EdgeLerpStatement.IsMatch(text))
            return null;
        text = WalkableColorStatement.Replace(text, $"float4 walkable_color = {fill};", 1);
        return EdgeLerpStatement.Replace(text, $"float4 walkability_map_color = lerp(walkable_color, {edge}, walkable_to_edge_ratio);", 1);
    }

    private static FileTransform Ascii(Func<string, string?> edit) => bytes =>
        edit(Encoding.ASCII.GetString(bytes)) is { } result ? Encoding.ASCII.GetBytes(result) : null;

    private static string Color(Rgb rgb, double gain, double opacity) =>
        $"float4({Number(rgb.R / 255.0 * gain)}f, {Number(rgb.G / 255.0 * gain)}f, {Number(rgb.B / 255.0 * gain)}f, {Number(Math.Clamp(opacity, 0, 1))}f)";

    // Always a decimal point and at least one decimal: "1" or "1,5" would not compile in the shader.
    private static string Number(double value) => value.ToString("0.0####", CultureInfo.InvariantCulture);

    public JsonObject? SaveSettings()
    {
        var settings = new JsonObject();
        if (Reveal != DefaultReveal) settings["reveal"] = Reveal;
        if (WalkableColor != DefaultWalkableColor) settings["walkableColor"] = WalkableColor.Hex;
        if (WalkableOpacity != DefaultWalkableOpacity) settings["walkableOpacity"] = WalkableOpacity;
        if (EdgeColor != DefaultEdgeColor) settings["edgeColor"] = EdgeColor.Hex;
        if (EdgeOpacity != DefaultEdgeOpacity) settings["edgeOpacity"] = EdgeOpacity;
        return settings.Count == 0 ? null : settings;
    }

    public void LoadSettings(JsonObject settings)
    {
        ResetSettings();
        Reveal = ReadFraction(settings, "reveal") ?? Reveal;
        WalkableOpacity = ReadFraction(settings, "walkableOpacity") ?? WalkableOpacity;
        EdgeOpacity = ReadFraction(settings, "edgeOpacity") ?? EdgeOpacity;
        if (settings["walkableColor"] is JsonValue w && w.TryGetValue<string>(out var wHex) && Rgb.TryParse(wHex, out var wRgb))
            WalkableColor = wRgb;
        if (settings["edgeColor"] is JsonValue e && e.TryGetValue<string>(out var eHex) && Rgb.TryParse(eHex, out var eRgb))
            EdgeColor = eRgb;
    }

    public void ResetSettings()
    {
        Reveal = DefaultReveal;
        WalkableColor = DefaultWalkableColor;
        WalkableOpacity = DefaultWalkableOpacity;
        EdgeColor = DefaultEdgeColor;
        EdgeOpacity = DefaultEdgeOpacity;
    }

    private static double? ReadFraction(JsonObject settings, string name) =>
        settings[name] is JsonValue v && v.TryGetValue<double>(out var d) && d is >= 0 and <= 1 ? d : null;
}
