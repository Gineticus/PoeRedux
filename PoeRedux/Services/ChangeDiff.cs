using System.Text;

namespace PoeRedux.Services;

/// <summary>
/// Formats one file's before/after bytes for the change review window. Kept separate from the
/// window itself so the encoding and binary-detection rules are testable without WPF.
/// </summary>
public static class ChangeDiff
{
    public static string Format(byte[] original, byte[] current)
    {
        if (!TryDecodeText(original, out var oldText) || !TryDecodeText(current, out var newText))
            return $"(binary: {original.Length:N0} -> {current.Length:N0} bytes)";

        var oldLines = oldText.Replace("\r\n", "\n").Split('\n');
        var newLines = newText.Replace("\r\n", "\n").Split('\n');
        var diff = TextDiff.Lines(oldLines, newLines);
        if (diff is null)
            return "(too many changed lines to show in full)";

        var sb = new StringBuilder();
        foreach (var line in diff)
        {
            if (line.Kind == TextDiff.LineKind.Same) continue;
            sb.AppendLine((line.Kind == TextDiff.LineKind.Added ? "+ " : "- ") + line.Text);
        }
        return sb.ToString();
    }

    /// <summary>
    /// A BOM marks the UTF-16LE files (.ot, .ao, statdescriptions .txt); everything else here is
    /// plain UTF-8/ASCII (.hlsl, .ffx). A .datc64 table decodes to a string full of control
    /// characters either way, which is how it is told apart from real patch text.
    /// </summary>
    public static bool TryDecodeText(byte[] bytes, out string text)
    {
        text = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE
            ? Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
            : Encoding.UTF8.GetString(bytes);
        return !text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    }
}
