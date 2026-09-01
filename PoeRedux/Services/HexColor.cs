using System.Globalization;

namespace PoeRedux.Services;

/// <summary>Parses "#RRGGBB", ignoring half typed values so a text box can bind live.</summary>
public static class HexColor
{
    public static bool TryParse(string value, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var text = value.Trim().TrimStart('#');
        return text.Length == 6
            && byte.TryParse(text[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(text[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(text[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
    }
}
