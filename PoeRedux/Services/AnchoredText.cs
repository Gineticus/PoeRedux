namespace PoeRedux.Services;

/// <summary>
/// Text edits that report themselves when the game files no longer contain what they look for.
/// A bare <c>string.Replace</c> returns the input unchanged when the literal moved, which is
/// indistinguishable from success at the call site.
/// </summary>
public static class AnchoredText
{
    public static string Replace(string data, string file, string find, string with)
    {
        // A patch runs against the current files, so its own previous output is not a miss.
        if (data.Contains(with, StringComparison.Ordinal))
            return data;

        var patched = data.Replace(find, with, StringComparison.Ordinal);
        if (patched == data)
            PatchReport.AnchorMissing(file, find);
        return patched;
    }
}
