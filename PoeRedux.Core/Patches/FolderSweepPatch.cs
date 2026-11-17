using LibBundle3.Nodes;
using PoeRedux.Helpers;
using System.Text.Json.Nodes;

namespace PoeRedux.Patches;

/// <summary>
/// A patch that changes every file of some types under one folder, except in the folders it keeps.
/// A kept entry is a path prefix: "a/boss" keeps a/boss/x.pet and also a/boss_room/x.pet. Before this was
/// configurable, that wider match kept a few more dangerous effects visible (maven_orb under maven, evil_red
/// under evil), so it stays.
/// </summary>
public abstract class FolderSweepPatch : IPatch, IPatchSettings
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract PatchGroup Group { get; }

    /// <summary>Folder the patch walks, lowercase and '/'-separated.</summary>
    public abstract string Root { get; }

    public abstract IReadOnlyList<string> Extensions { get; }

    /// <summary>Kept folders out of the box: where the effect is the warning, such as boss attacks and dangerous ground.</summary>
    public abstract IReadOnlyList<string> DefaultKept { get; }

    /// <summary>Folders left as they are.</summary>
    public SortedSet<string> Kept { get; } = new(StringComparer.Ordinal);

    // Field initializers of a derived class run before this constructor, so DefaultKept is already set.
    protected FolderSweepPatch() => ResetSettings();

    /// <summary>The new content of <paramref name="file"/>.</summary>
    protected abstract FileTransform TransformFor(FileNode file);

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, Root), Extensions.ToArray()))
        {
            var path = file.Record.Path;
            if (!Kept.Any(k => path.StartsWith(k, StringComparison.Ordinal)))
                plan.Add(file, TransformFor(file));
        }
    }

    public JsonObject? SaveSettings()
    {
        var defaults = DefaultKept.ToHashSet(StringComparer.Ordinal);
        var added = Kept.Where(k => !defaults.Contains(k)).Select(k => (JsonNode?)k).ToArray();
        var removed = DefaultKept.Where(k => !Kept.Contains(k)).Select(k => (JsonNode?)k).ToArray();
        if (added.Length == 0 && removed.Length == 0)
            return null;
        var settings = new JsonObject();
        if (added.Length > 0) settings["keep"] = new JsonArray(added);
        if (removed.Length > 0) settings["dontKeep"] = new JsonArray(removed);
        return settings;
    }

    public void LoadSettings(JsonObject settings)
    {
        ResetSettings();
        foreach (var path in Strings(settings["keep"]))
        {
            if (path.StartsWith(Root + "/", StringComparison.Ordinal))
                Kept.Add(path);
        }
        foreach (var path in Strings(settings["dontKeep"]))
            Kept.Remove(path);
    }

    public void ResetSettings()
    {
        Kept.Clear();
        foreach (var path in DefaultKept)
            Kept.Add(path);
    }

    private static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? array.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()
            : [];
}
