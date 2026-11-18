using System.Text.RegularExpressions;

namespace PoeRedux.Data;

/// <summary>
/// One description block of a stat description file (.csd): the stats it describes and its English lines.
/// The first stat names the block; Color Mods matches on it.
/// </summary>
/// <param name="Stats">Stat ids, in the order the block lists them.</param>
/// <param name="English">The English variants, each with the value range it applies to.</param>
public sealed record StatDescription(IReadOnlyList<string> Stats, IReadOnlyList<StatText> English)
{
    public string Key => Stats[0];
}

/// <param name="Ranges">One range per stat, such as "1|#", "#|-1" or "#".</param>
/// <param name="Text">The text with {0}-style placeholders and the game's [Keyword|Shown text] markup.</param>
/// <param name="Handlers">Words after the text, such as "negate 1" or "divide_by_one_hundred 1", that change how values show.</param>
public sealed record StatText(IReadOnlyList<string> Ranges, string Text, IReadOnlyList<string> Handlers);

/// <summary>
/// Reads the format of data/statdescriptions (PoE 2) and metadata/statdescriptions (PoE 1):
/// <code>
/// description
/// 	2 stat_a stat_b
/// 	1
/// 		1|# # "Text {0}" handler
/// 	lang "German"
/// 	...
/// </code>
/// Only the English lines, the ones before the first lang line, are kept.
/// </summary>
public sealed class StatDescriptionFile
{
    private static readonly Regex Quoted = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    public List<StatDescription> Descriptions { get; } = [];

    /// <summary>Stats listed after no_description: the game shows nothing for them.</summary>
    public HashSet<string> Hidden { get; } = new(StringComparer.Ordinal);

    /// <summary>Paths named by include lines, as written.</summary>
    public List<string> Includes { get; } = [];

    public static StatDescriptionFile Parse(string text)
    {
        var file = new StatDescriptionFile();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith("no_description", StringComparison.Ordinal))
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    file.Hidden.Add(parts[1]);
            }
            else if (line.StartsWith("include", StringComparison.Ordinal))
            {
                if (Quoted.Match(line) is { Success: true } m)
                    file.Includes.Add(m.Groups[1].Value);
            }
            else if (line.StartsWith("description", StringComparison.Ordinal) && i + 2 < lines.Length)
            {
                var statParts = lines[i + 1].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (statParts.Length < 2 || !int.TryParse(statParts[0], out var statCount))
                    continue;
                var stats = statParts.Skip(1).Take(statCount).ToList();

                var countParts = lines[i + 2].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (countParts.Length == 0 || !int.TryParse(countParts[0], out var textCount))
                    continue;

                var english = new List<StatText>(textCount);
                for (var k = 0; k < textCount && i + 3 + k < lines.Length; k++)
                {
                    var textLine = lines[i + 3 + k];
                    var m = Quoted.Match(textLine);
                    if (!m.Success)
                        continue;
                    var ranges = textLine[..m.Index].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
                    var handlers = textLine[(m.Index + m.Length)..].Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
                    english.Add(new StatText(ranges, m.Groups[1].Value, handlers));
                }
                file.Descriptions.Add(new StatDescription(stats, english));
                i += 2 + textCount;
            }
        }
        return file;
    }
}
