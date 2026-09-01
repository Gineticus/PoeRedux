using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using LibBundle3.Nodes;

using PoeRedux.Services;

namespace PoeRedux.Patches;

public class Minimap : IPatch
{
    public string Name => "Minimap Patch";
    public object Description => "Reveals the entire minimap by default.";

    /// <summary>
    /// Floor under the visibility channel, so terrain nobody walked still draws. 0 leaves the
    /// minimap as the game ships it, 1 reveals everything at full strength.
    /// </summary>
    public double RevealStrength { get; set; } = DefaultRevealStrength;

    /// <summary>Fill of the walkable interior.</summary>
    public string WalkableHex { get; set; } = DefaultWalkableHex;

    public double WalkableOpacity { get; set; } = DefaultWalkableOpacity;

    /// <summary>Color the fill fades into at walls and edges.</summary>
    public string EdgeHex { get; set; } = DefaultEdgeHex;

    public double EdgeOpacity { get; set; } = DefaultEdgeOpacity;

    public const double DefaultRevealStrength = 0.18;
    public const string DefaultWalkableHex = "#000000";
    public const double DefaultWalkableOpacity = 0.3;
    public const string DefaultEdgeHex = "#FFFFFF";
    public const double DefaultEdgeOpacity = 0.1;

    /// <summary>
    /// Edge color multiplier. The shader works in linear HDR, where the shipped edge color sits
    /// well above 1, and that overbright is what makes walls read against the fill.
    /// </summary>
    private const double EdgeGain = 12.0;

    public void Apply(DirectoryNode root)
    {
        var shaders = NavigateTo(root, "shaders");
        if (shaders is null)
        {
            PatchReport.FileMissing("shaders");
            return;
        }

        var files = shaders.Children.OfType<FileNode>().ToDictionary(f => f.Name, StringComparer.Ordinal);

        if (files.TryGetValue(VisibilityShader, out var visibility))
            Rewrite(visibility, RevealFloor);
        else
            PatchReport.FileMissing("shaders/" + VisibilityShader);

        if (files.TryGetValue(BlendingShader, out var blending))
            Rewrite(blending, Recolor);
        else
            PatchReport.FileMissing("shaders/" + BlendingShader);
    }

    private static void Rewrite(FileNode file, Func<string, string> rewrite)
    {
        var record = file.Record;
        var data = Encoding.ASCII.GetString(record.Read().ToArray());
        var patched = rewrite(data);
        if (patched == data)
            return;

        BackupManager.RecordOriginal(record);
        record.Write(Encoding.ASCII.GetBytes(patched));
    }

    /// <summary>
    /// Adds a floor to the visibility channel. The line goes after the fully revealed assignment,
    /// which has no braces around it, so the floor lands outside that branch and applies to every
    /// pixel. That is the whole reveal.
    /// </summary>
    public string RevealFloor(string data)
    {
        var line = $"\tres_color = max(res_color, {Number(RevealStrength)}f);";
        var existing = FloorLine.Match(data);
        if (existing.Success)
            return data.Replace(existing.Value, line, StringComparison.Ordinal);

        var lines = data.Split("\r\n").ToList();
        var index = lines.FindIndex(l => l.Contains(FullyRevealed, StringComparison.Ordinal));
        if (index < 0)
        {
            PatchReport.AnchorMissing(VisibilityShader, FullyRevealed);
            return data;
        }
        lines.Insert(index + 1, line);
        return string.Join("\r\n", lines);
    }

    public string Recolor(string data)
    {
        var fill = Color(WalkableHex, DefaultWalkableHex, 1.0, WalkableOpacity);
        var edge = Color(EdgeHex, DefaultEdgeHex, EdgeGain, EdgeOpacity);

        var patched = Substitute(data, WalkableColor, $"float4 walkable_color = {fill};");
        patched = Substitute(patched, EdgeLerp,
            $"float4 walkability_map_color = lerp(walkable_color, {edge}, walkable_to_edge_ratio);");
        return patched;
    }

    /// <summary>
    /// Matching the whole statement rather than the shipped literal means a second apply rewrites
    /// its own output instead of stacking a duplicate, and a game update that only retunes the
    /// numbers still matches.
    /// </summary>
    private static string Substitute(string data, Regex statement, string replacement)
    {
        if (!statement.IsMatch(data))
        {
            PatchReport.AnchorMissing(BlendingShader, statement.ToString());
            return data;
        }
        return statement.Replace(data, replacement.Replace("$", "$$"), 1);
    }

    private static string Color(string hex, string fallback, double gain, double opacity)
    {
        if (!HexColor.TryParse(hex, out var r, out var g, out var b))
            HexColor.TryParse(fallback, out r, out g, out b);
        return $"float4({Number(r / 255.0 * gain)}f, {Number(g / 255.0 * gain)}f, " +
            $"{Number(b / 255.0 * gain)}f, {Number(opacity)}f)";
    }

    /// <summary>A comma decimal separator would not compile in the shader.</summary>
    private static string Number(double value) =>
        value.ToString("0.0####", CultureInfo.InvariantCulture);

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

    private const string VisibilityShader = "minimap_visibility_pixel.hlsl";
    private const string BlendingShader = "minimap_blending_pixel.hlsl";
    private const string FullyRevealed = "res_color = float4(1.0f, 0.0f, 0.0f, 1.0f);";

    private static readonly Regex FloorLine =
        new(@"[ \t]*res_color = max\(res_color, [0-9.]+f\);", RegexOptions.Compiled);

    private static readonly Regex WalkableColor =
        new(@"float4 walkable_color = float4\([^;]*\);", RegexOptions.Compiled);

    private static readonly Regex EdgeLerp =
        new(@"float4 walkability_map_color = lerp\(walkable_color, float4\([^)]*\), walkable_to_edge_ratio\);",
            RegexOptions.Compiled);
}
