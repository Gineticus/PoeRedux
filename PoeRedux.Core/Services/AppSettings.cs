using PoeRedux.Models;
using PoeRedux.Patches;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PoeRedux.Services;

/// <summary>
/// What PoE Redux remembers between runs: the selected game, and per game the file, the ticked patches and
/// each patch's options. Stored as JSON in %LOCALAPPDATA%\PoeRedux\settings.json.
/// </summary>
public sealed class AppSettings
{
    private const int Version = 1;

    public static string FilePath { get; set; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "settings.json");

    public PoeGame Game { get; set; } = PoeGame.PoE1;

    public Dictionary<PoeGame, GameSettings> Games { get; } = new()
    {
        [PoeGame.PoE1] = new GameSettings(),
        [PoeGame.PoE2] = new GameSettings(),
    };

    public GameSettings For(PoeGame game) => Games[game];

    /// <summary>Reads the settings file; a missing or unreadable file gives the defaults.</summary>
    public static AppSettings Load()
    {
        var settings = new AppSettings();
        if (!File.Exists(FilePath))
            return settings;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(FilePath)) is not JsonObject root)
                return settings;
            if (root["game"] is JsonValue g && g.TryGetValue<string>(out var game) && Enum.TryParse<PoeGame>(game, out var parsed))
                settings.Game = parsed;
            if (root["games"] is JsonObject games)
            {
                foreach (var key in Enum.GetValues<PoeGame>())
                {
                    if (games[key.ToString()] is JsonObject saved)
                        settings.Games[key] = GameSettings.FromJson(saved);
                }
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the unreadable file for a bug report and start from the defaults.
            try { File.Copy(FilePath, FilePath + ".unreadable", true); } catch (IOException) { }
        }
        return settings;
    }

    public void Save()
    {
        var games = new JsonObject();
        foreach (var (game, settings) in Games)
            games[game.ToString()] = settings.ToJson();
        var root = new JsonObject
        {
            ["version"] = Version,
            ["game"] = Game.ToString(),
            ["games"] = games,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
}

public sealed class GameSettings
{
    /// <summary>Content.ggpk or _.index.bin.</summary>
    public string Path { get; set; } = "";

    /// <summary>Ids of the ticked patches, or null when the user never changed the selection.</summary>
    public HashSet<string>? Selected { get; set; }

    /// <summary>Options per patch id, as <see cref="IPatchSettings.SaveSettings"/> wrote them.</summary>
    public Dictionary<string, JsonObject> PatchSettings { get; } = new(StringComparer.Ordinal);

    /// <summary>Copies the saved options into <paramref name="patches"/>.</summary>
    public void ApplyTo(IEnumerable<IPatch> patches)
    {
        foreach (var patch in patches.OfType<IPatchSettings>())
        {
            var id = ((IPatch)patch).Id;
            if (PatchSettings.TryGetValue(id, out var saved))
                patch.LoadSettings(saved.DeepClone().AsObject());
            else
                patch.ResetSettings();
        }
    }

    /// <summary>Takes the options and selection from <paramref name="patches"/>.</summary>
    public void CaptureFrom(IEnumerable<IPatch> patches, IEnumerable<string> selectedIds)
    {
        PatchSettings.Clear();
        foreach (var patch in patches)
        {
            if (patch is IPatchSettings settings && settings.SaveSettings() is { } saved)
                PatchSettings[patch.Id] = saved;
        }
        Selected = selectedIds.ToHashSet(StringComparer.Ordinal);
    }

    internal JsonObject ToJson()
    {
        var json = new JsonObject { ["path"] = Path };
        if (Selected is not null)
            json["selected"] = new JsonArray(Selected.Order(StringComparer.Ordinal).Select(s => (JsonNode?)s).ToArray());
        if (PatchSettings.Count > 0)
        {
            var patches = new JsonObject();
            foreach (var (id, settings) in PatchSettings.OrderBy(p => p.Key, StringComparer.Ordinal))
                patches[id] = settings.DeepClone();
            json["patches"] = patches;
        }
        return json;
    }

    internal static GameSettings FromJson(JsonObject json)
    {
        var settings = new GameSettings();
        if (json["path"] is JsonValue p && p.TryGetValue<string>(out var path))
            settings.Path = path;
        if (json["selected"] is JsonArray selected)
            settings.Selected = selected.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if (json["patches"] is JsonObject patches)
        {
            foreach (var (id, node) in patches)
            {
                if (node is JsonObject o)
                    settings.PatchSettings[id] = o.DeepClone().AsObject();
            }
        }
        return settings;
    }
}
