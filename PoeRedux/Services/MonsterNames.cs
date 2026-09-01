using System.IO;
using System.Text.Json;

namespace PoeRedux.Services;

/// <summary>
/// Monster display names to the effect folders named after their family, generated offline by
/// <c>GgpkStudy monsters</c>. The names live in a dat table whose 794 byte row needs a schema that
/// drifts every league, so the app reads a plain list instead of the table.
/// </summary>
public static class MonsterNames
{
    /// <summary>What a search box query turned out to mean.</summary>
    public sealed record Match(string Query, string? Name, string? Family, IReadOnlyList<string> Folders)
    {
        public bool Resolved => Family is not null;

        /// <summary>What the search actually looked for, which is the family, not the typed name.</summary>
        public string Needle => Family ?? Query;
    }

    private sealed class Map
    {
        public Dictionary<string, List<string>> Families { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Monsters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static Map? _map;

    private static Map Load()
    {
        if (_map is not null)
            return _map;
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "monster-effects.json");
            using var stream = File.OpenRead(path);
            _map = JsonSerializer.Deserialize<Map>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new Map();
        }
        catch
        {
            // Without the map the picker still browses and searches by folder name.
            _map = new Map();
        }
        return _map;
    }

    /// <summary>
    /// Turns anything a poedb monster page offers into the family its effect folders are named
    /// after: a display name, the Metadata/Monsters path, or one of its skill ids.
    /// </summary>
    public static Match Resolve(string query)
    {
        var map = Load();
        var text = query.Trim();
        if (text.Length < 3)
            return new Match(text, null, null, []);

        if (map.Monsters.TryGetValue(text, out var family))
            return Found(map, text, text, family);

        if (FromMetadataPath(text) is string fromPath && map.Families.ContainsKey(fromPath))
            return Found(map, text, null, fromPath);

        // "Wretched Def" while still typing.
        var byPrefix = map.Monsters.Keys
            .Where(n => n.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Length)
            .FirstOrDefault();
        if (byPrefix is not null)
            return Found(map, text, byPrefix, map.Monsters[byPrefix]);

        // A skill id such as RevenantMapBossSummon1: only the family part of it exists in a path,
        // so the id is split on its capitals and each piece tried against the known families.
        foreach (var word in Words(text))
            if (map.Families.ContainsKey(word))
                return Found(map, text, null, word);

        return new Match(text, null, null, []);
    }

    private static Match Found(Map map, string query, string? name, string family) =>
        new(query, name, family, map.Families.GetValueOrDefault(family) ?? []);

    /// <summary>Metadata/Monsters/&lt;family&gt;/&lt;name&gt;, which is the line poedb shows.</summary>
    private static string? FromMetadataPath(string text)
    {
        const string prefix = "metadata/monsters/";
        var normalized = text.Replace('\\', '/');
        var at = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return null;
        var rest = normalized[(at + prefix.Length)..].Split('/');
        return rest.Length > 0 && rest[0].Length > 0 ? rest[0] : null;
    }

    /// <summary>The words inside a run-together id, longest first so the family beats "boss".</summary>
    private static IEnumerable<string> Words(string text)
    {
        var word = new System.Text.StringBuilder();
        var words = new List<string>();
        foreach (var c in text)
        {
            if (char.IsUpper(c) || !char.IsLetter(c))
            {
                if (word.Length > 3)
                    words.Add(word.ToString().ToLowerInvariant());
                word.Clear();
            }
            if (char.IsLetter(c))
                word.Append(c);
        }
        if (word.Length > 3)
            words.Add(word.ToString().ToLowerInvariant());
        return words.OrderByDescending(w => w.Length);
    }
}
