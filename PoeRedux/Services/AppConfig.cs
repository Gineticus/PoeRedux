using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoeRedux.Services;

/// <summary>
/// Everything the window should remember between launches, in one file. Anything that can be
/// derived from the game files is stored as a delta from the default instead of in full, so a
/// game update does not resurrect a stale list.
/// </summary>
public class AppConfig
{
    public int SelectedGame { get; set; }

    /// <summary>Keyed by <see cref="PoeGame"/> name, so both installs can be remembered.</summary>
    public Dictionary<string, string> GamePaths { get; set; } = new(StringComparer.Ordinal);

    public double CameraZoom { get; set; } = 2.4;

    /// <summary>Patch display names that were checked, per game.</summary>
    public Dictionary<string, List<string>> SelectedPatches { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The patch server version the last successful Apply ran against, per game. Set only when
    /// that run actually reached the server, so a version this tool never confirmed never masks
    /// a real update. Compared against the server's current version to tell the user the game
    /// moved out from under their patches.
    /// </summary>
    public Dictionary<string, string> LastAppliedVersion { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Environment switches the user turned off, keyed "game:patch". Only the delta, since every
    /// switch is on by default and a switch added later should start on.
    /// </summary>
    public Dictionary<string, List<string>> EnvKeysOff { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Effect and particle folders the user switched off, keyed "game:patch". The delta again, so
    /// a folder a later league adds inherits its parent instead of arriving excluded.
    /// </summary>
    public Dictionary<string, List<string>> SweepFoldersOff { get; set; } = new(StringComparer.Ordinal);

    /// <summary>How the minimap reveal draws: the visibility floor and the two shader colors.</summary>
    public double MinimapReveal { get; set; } = 0.18;

    public string MinimapWalkableHex { get; set; } = "#000000";

    public double MinimapWalkableOpacity { get; set; } = 0.3;

    public string MinimapEdgeHex { get; set; } = "#FFFFFF";

    public double MinimapEdgeOpacity { get; set; } = 0.1;

    /// <summary>Group name to "#RRGGBB", for the mod color groups.</summary>
    public Dictionary<string, string> ModGroups { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Group name to the short label it writes in tag mode.</summary>
    public Dictionary<string, string> ModGroupTags { get; set; } = new(StringComparer.Ordinal);

    public bool ModTagPrefix { get; set; }

    public bool TierTagPrefix { get; set; }

    public bool TierPerLadder { get; set; } = true;

    public double TierMinLadderShare { get; set; } = 0.5;

    public bool TierCompactTier { get; set; }

    /// <summary>The whole curated mod list, since entries can be added and removed freely.</summary>
    public Dictionary<string, List<ColorModEntry>> ColorMods { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The quality bands: name, lowest percent of the best roll, color, on or off.</summary>
    public List<RollTierEntry> RollTiers { get; set; } = [];

    public class RollTierEntry
    {
        public string Name { get; set; } = "";
        public string Percent { get; set; } = "";
        public string Hex { get; set; } = "";
        public string Tag { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Only the stats that were unchecked, since everything is graded by default.</summary>
    public List<string> TierStatsDisabled { get; set; } = [];

    /// <summary>
    /// The vanilla icon of every object type that draws one, cached from the last scan. Without
    /// it the vanilla identity is lost the moment a patch overwrites the value in the ggpk.
    /// </summary>
    public Dictionary<string, string> MinimapVanilla { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The chosen restyle per vanilla icon, and whether it is switched on.</summary>
    public List<MinimapIconEntry> MinimapIcons { get; set; } = [];

    public class MinimapIconEntry
    {
        public string Vanilla { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Category { get; set; } = "General";
        public bool Enabled { get; set; }

        /// <summary>Object type path to icon id, for the paths that opted out of the group's icon.</summary>
        public Dictionary<string, string> PathOverrides { get; set; } = new(StringComparer.Ordinal);
    }

    public class ColorModEntry
    {
        public string Id { get; set; } = "";
        public string Group { get; set; } = "";
        public bool Enabled { get; set; }
        public string Category { get; set; } = "Other";
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "config.json");

    public static AppConfig Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Options) ?? new()
                : new();
        }
        catch
        {
            // A hand edited or truncated config must not stop the app from opening.
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Losing settings is not worth interrupting whatever the user was doing.
        }
    }
}
