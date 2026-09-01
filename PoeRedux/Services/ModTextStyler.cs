using System.Text.RegularExpressions;

namespace PoeRedux.Services;

/// <summary>
/// Rewrites one display string in a StatDescriptions file. Shared by the PoE 1 and PoE 2 mod
/// color patches so both styles behave the same and neither drifts.
/// </summary>
public static class ModTextStyler
{
    private static readonly Regex Quoted = new("\"(?<text>.*)\"", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OurMarkup =
        new(@"^<rgb\([^)]*\)>\{\{(?<inner>.*?)\}\}(?<rest>.*)$", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// The vanilla string, with whatever this tool wrote last time taken back off. Both styles
    /// have to be undoable, or switching between them, or turning a mod off, leaves debris.
    /// </summary>
    public static string Strip(string content)
    {
        var match = OurMarkup.Match(content);
        if (!match.Success)
            return content;
        var rest = match.Groups["rest"].Value;
        // Text after the styled run means the run was a tag; otherwise it held the mod itself.
        // Recursing handles tag mode's second run, which restyles the body back to normal.
        return rest.Length > 0 ? Strip(rest.TrimStart()) : match.Groups["inner"].Value;
    }

    /// <summary>
    /// Restyles the quoted string on a range line, leaving the range prefix and any trailing
    /// keyword such as `markup` untouched.
    /// </summary>
    /// <summary>
    /// The color the rest of the line is set back to in tag mode. An unstyled remainder inherits
    /// the tag's color, so the body has to be given a color of its own to stop the bleed.
    /// Roughly the client's own mod text blue.
    /// </summary>
    public const string BodyAnnotation = "rgb(136,136,255)";

    public static string Restyle(string line, string? annotation, string tag, bool enabled, bool tagPrefix)
    {
        var match = Quoted.Match(line);
        if (!match.Success)
            return line;

        var plain = Strip(match.Groups["text"].Value);
        string styled;
        if (!enabled || annotation is null)
            styled = plain;
        // Whole line mode cannot color a line that already carries the game's own markup without
        // overwriting the style name, which is unrecoverable. Tag those instead.
        else if (tagPrefix || (plain.Contains('<') && tag.Length > 0))
            styled = plain.Contains('<')
                // Already has a styled run of its own, which ends the tag's color for us.
                ? $"<{annotation}>{{{{{tag}}}}} {plain}"
                : $"<{annotation}>{{{{{tag}}}}} <{BodyAnnotation}>{{{{{plain}}}}}";
        else if (plain.Contains('<'))
            styled = plain;
        else
            styled = $"<{annotation}>{{{{{plain}}}}}";

        return line[..match.Index] + "\"" + styled + "\"" + line[(match.Index + match.Length)..];
    }
}
