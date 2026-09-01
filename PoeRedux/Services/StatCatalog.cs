using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using LibBundledGGPK3;

using PoeRedux.Models;

namespace PoeRedux.Services;

/// <summary>
/// Every stat id the game has a description for, read straight from the selected game
/// files. A stat id with no description never renders as text, so it cannot be colored.
/// </summary>
public static class StatCatalog
{
    private static readonly Regex Quoted = new("\"([^\"]*)\"", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CategoryByFile = new(StringComparer.OrdinalIgnoreCase)
    {
        ["map_stat_descriptions.txt"] = "Map / Atlas",
        ["atlas_stat_descriptions.txt"] = "Map / Atlas",
        ["chest_stat_descriptions.txt"] = "Strongbox",
        ["primordial_altar_stat_descriptions.txt"] = "Altar",
        ["expedition_relic_stat_descriptions.txt"] = "Expedition",
        ["monster_stat_descriptions.txt"] = "Monster",
        ["sanctum_relic_stat_descriptions.txt"] = "Sanctum",
        ["necropolis_stat_descriptions.txt"] = "Necropolis",
        ["heist_equipment_stat_descriptions.txt"] = "Heist",
        ["sentinel_stat_descriptions.txt"] = "Sentinel",
        ["leaguestone_stat_descriptions.txt"] = "Leaguestone",
        ["stat_descriptions.txt"] = "Item / General",
    };

    public static Task<List<StatCatalogEntry>> LoadAsync(string gameFilePath) =>
        Task.Run(() => Load(gameFilePath));

    private static List<StatCatalogEntry> Load(string gameFilePath)
    {
        var byId = GameFile.Read(gameFilePath, index =>
        {
            var found = new Dictionary<string, StatCatalogEntry>(StringComparer.Ordinal);
            Scan(index, found);
            return found;
        });

        return byId.Values
            .OrderBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Stat id to the text shown in game, for callers that only need labels.</summary>
    public static Dictionary<string, string> Scan(LibBundle3.Index index)
    {
        var byId = new Dictionary<string, StatCatalogEntry>(StringComparer.Ordinal);
        Scan(index, byId);
        return byId.ToDictionary(kv => kv.Key, kv => kv.Value.Text, StringComparer.Ordinal);
    }

    private static void Scan(LibBundle3.Index index, Dictionary<string, StatCatalogEntry> byId)
    {
        foreach (var file in index.Files.Values)
        {
            var path = file.Path;
            if (!path.StartsWith("metadata/statdescriptions/", StringComparison.Ordinal)
                || !path.EndsWith(".txt", StringComparison.Ordinal))
                continue;

            var fileName = Path.GetFileName(path);
            var category = CategoryFor(fileName);
            var lines = Encoding.Unicode.GetString(file.Read().Span).Split("\r\n");

            for (var i = 0; i < lines.Length - 1; i++)
            {
                if (!lines[i].StartsWith("description", StringComparison.Ordinal))
                    continue;

                // The line after "description" is "<count> <stat_id> [stat_id ...]".
                var parts = lines[i + 1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[0], out var count))
                    continue;

                // Then a range-count line, then one quoted display string per value range.
                var sample = "";
                for (var j = i + 2; j < Math.Min(i + 8, lines.Length); j++)
                {
                    var m = Quoted.Match(lines[j]);
                    if (m.Success)
                    {
                        sample = m.Groups[1].Value.Replace("\\n", " ");
                        break;
                    }
                }

                foreach (var id in parts.Skip(1).Take(count))
                {
                    if (byId.TryGetValue(id, out var existing))
                    {
                        // A generic description is a weaker category hint than a specific file.
                        if (existing.Category == "Item / General" && category != "Item / General")
                            byId[id] = new StatCatalogEntry(id, existing.Text, category);
                        continue;
                    }
                    byId[id] = new StatCatalogEntry(id, sample, category);
                }
            }
        }
    }

    private static string CategoryFor(string fileName)
    {
        if (CategoryByFile.TryGetValue(fileName, out var known))
            return known;
        var stem = fileName.Replace("_stat_descriptions.txt", "").Replace('_', ' ');
        return stem.Length == 0 ? "Other" : char.ToUpperInvariant(stem[0]) + stem[1..];
    }
}
