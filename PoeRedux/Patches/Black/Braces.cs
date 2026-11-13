namespace PoeRedux.Patches.Black;

internal static class Braces
{
    /// <summary>
    /// Replaces the first <c>{ ... }</c> block at or after <paramref name="from"/>, braces included, with
    /// <paramref name="replacement"/>. Every brace counts, even one inside a string or comment.
    /// Returns <paramref name="text"/> unchanged when <paramref name="from"/> is negative or no block closes.
    /// </summary>
    public static string ReplaceBlock(string text, int from, string replacement)
    {
        if (from < 0)
            return text;

        int open = text.IndexOf('{', from);
        if (open < 0)
            return text;

        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;

            if (depth == 0)
                return text[..open] + replacement + text[(i + 1)..];
        }
        return text;
    }
}
