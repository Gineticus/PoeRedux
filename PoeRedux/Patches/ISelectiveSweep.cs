namespace PoeRedux.Patches;

/// <summary>
/// A sweep that can be pointed at part of its tree instead of all of it. The unit is a folder
/// prefix, because effect and particle files are grouped by the monster or skill they belong to
/// and nothing in the game data links a file to its owner any more directly than that.
/// </summary>
public interface ISelectiveSweep
{
    /// <summary>Index path the sweep walks, lowercase and slash separated.</summary>
    string Root { get; }

    /// <summary>File extensions the sweep rewrites. Everything else under the root is left alone.</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Folder prefixes to leave alone. Seeded with the paths where the visual is the mechanic, so
    /// the shipped defaults behave as they always did, and stored as the delta for the same reason.
    /// </summary>
    HashSet<string> DisabledPrefixes { get; }
}
