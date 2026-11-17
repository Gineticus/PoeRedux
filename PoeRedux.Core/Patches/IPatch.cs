using LibBundle3.Nodes;
using System.Text.Json.Nodes;

namespace PoeRedux.Patches;

/// <summary>Where a patch is listed.</summary>
public enum PatchGroup
{
    Readability,
    MapAndCamera,
    VisualNoise,
    Experimental,
}

public interface IPatch
{
    /// <summary>Stable key for saved settings. Never shown and never changed once released.</summary>
    string Id { get; }

    string Name { get; }
    string Description { get; }
    PatchGroup Group { get; }

    /// <summary>
    /// Adds the files this patch changes to <paramref name="plan"/>. Called once per run with a fresh tree.
    /// The transforms run later on worker threads, so they must not change shared state.
    /// </summary>
    void Collect(DirectoryNode root, PatchPlan plan);
}

/// <summary>A patch with options the user can change.</summary>
public interface IPatchSettings
{
    /// <summary>The options that differ from the defaults, or null when none do.</summary>
    JsonObject? SaveSettings();

    /// <summary>Applies options saved by <see cref="SaveSettings"/>. Unknown or invalid values are ignored.</summary>
    void LoadSettings(JsonObject settings);

    /// <summary>Puts every option back to its default.</summary>
    void ResetSettings();
}
