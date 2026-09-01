namespace PoeRedux.Services;

/// <summary>
/// One value range line of a StatDescriptions block, such as `1|# "text"` or `#|-1 "text"`.
/// A null bound is the file's `#`, meaning open ended in that direction.
/// </summary>
public sealed record ValueRange(int? Lo, int? Hi, string Body, string Trailer)
{
    public static bool TryParse(string line, out ValueRange range)
    {
        range = new ValueRange(null, null, "", "");

        var quoteStart = line.IndexOf('"');
        var quoteEnd = line.LastIndexOf('"');
        if (quoteStart < 0 || quoteEnd <= quoteStart)
            return false;

        var token = line[..quoteStart].Trim();
        var body = line[(quoteStart + 1)..quoteEnd];
        var trailer = line[(quoteEnd + 1)..];

        if (token == "#")
        {
            range = new ValueRange(null, null, body, trailer);
            return true;
        }

        var parts = token.Split('|');
        if (parts.Length != 2)
            return false;
        if (!TryBound(parts[0], out var lo) || !TryBound(parts[1], out var hi))
            return false;

        range = new ValueRange(lo, hi, body, trailer);
        return true;
    }

    private static bool TryBound(string text, out int? bound)
    {
        text = text.Trim();
        if (text == "#")
        {
            bound = null;
            return true;
        }
        if (int.TryParse(text, out var value))
        {
            bound = value;
            return true;
        }
        bound = null;
        return false;
    }

    public static string Format(int? lo, int? hi, string body, string trailer) =>
        $"\t\t{Token(lo, hi)} \"{body}\"{trailer}";

    private static string Token(int? lo, int? hi) => (lo, hi) switch
    {
        (null, null) => "#",
        (not null, null) => $"{lo}|#",
        (null, not null) => $"#|{hi}",
        _ => $"{lo}|{hi}",
    };
}
