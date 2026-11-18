using System.Text.Json.Nodes;

namespace PoeRedux.Patches;

/// <summary>One part of a patch that can be turned off on its own.</summary>
/// <param name="Id">Stable key for saved settings.</param>
/// <param name="Name">What the player sees in game, such as "Fog".</param>
public sealed record PatchSwitch(string Id, string Name, Func<string, string> Edit);

/// <summary>
/// A patch made of independent switches, all on by default. The settings store the switches turned off,
/// so a switch added in a later version starts on.
/// </summary>
public abstract class SwitchPatch : IPatch, IPatchSettings
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract PatchGroup Group { get; }
    public abstract IReadOnlyList<PatchSwitch> Switches { get; }

    /// <summary>Ids of the switches turned off.</summary>
    public HashSet<string> Off { get; } = new(StringComparer.Ordinal);

    public IEnumerable<PatchSwitch> OnSwitches => Switches.Where(s => !Off.Contains(s.Id));

    public abstract void Collect(LibBundle3.Nodes.DirectoryNode root, PatchPlan plan);

    /// <summary>The text after every switch that is on has edited it.</summary>
    protected string ApplySwitches(string text) => OnSwitches.Aggregate(text, (current, s) => s.Edit(current));

    public JsonObject? SaveSettings()
    {
        var off = Switches.Where(s => Off.Contains(s.Id)).Select(s => (JsonNode?)s.Id).ToArray();
        return off.Length == 0 ? null : new JsonObject { ["off"] = new JsonArray(off) };
    }

    public void LoadSettings(JsonObject settings)
    {
        ResetSettings();
        if (settings["off"] is not JsonArray off)
            return;
        var known = Switches.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var node in off)
        {
            if (node is JsonValue v && v.TryGetValue<string>(out var id) && known.Contains(id))
                Off.Add(id);
        }
    }

    public void ResetSettings() => Off.Clear();
}
