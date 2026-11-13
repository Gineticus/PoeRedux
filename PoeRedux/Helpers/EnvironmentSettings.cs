using LibBundle3.Nodes;
using PoeRedux.Patches;
using System.Text.RegularExpressions;

namespace PoeRedux.Helpers;

/// <summary>
/// Edits of .env files, the JSON-like area settings for fog, light, weather and other effects.
/// </summary>
internal static class EnvironmentSettings
{
    /// <summary>
    /// Adds <paramref name="edit"/> for every .env file under metadata/environmentsettings.
    /// </summary>
    public static void EditAll(DirectoryNode root, PatchPlan plan, Func<string, string> edit)
    {
        var transform = Utf16.Edit(edit);
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, "metadata/environmentsettings"), ".env"))
            plan.Add(file, transform);
    }

    /// <summary>
    /// Renames the keys so the game no longer finds these settings: "fog" becomes "xog".
    /// </summary>
    public static string DisableKeys(string text, params string[] keys)
    {
        foreach (var key in keys)
            text = text.Replace($"\"{key}\"", $"\"x{key[1..]}\"");
        return text;
    }

    public static string DisableShadows(string text) =>
        text.Replace("\"shadows_enabled\": true", "\"shadows_enabled\": false");

    /// <summary>
    /// Sets the value of <paramref name="key"/> to 0.0: "rain_intensity": 0.8 becomes "rain_intensity": 0.0.
    /// </summary>
    public static string SetToZero(string text, string key) =>
        Regex.Replace(text, $@"(""{key}"":\s*)([^,\r\n}}]+)(,?)", "${1}0.0${3}");
}
