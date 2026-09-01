namespace PoeRedux.Services;

/// <summary>
/// Anchors a patch looked for and did not find. A patch locates its work by a literal from the
/// game files, so a game update that moves one leaves the patch with nothing to do; every patch
/// used to return quietly while the apply still reported success. Ambient like
/// <see cref="BackupManager"/>, which keeps a miss to one call at the bail site instead of a
/// signature change through every patch.
/// </summary>
public static class PatchReport
{
    public sealed record Miss(string Patch, string Detail);

    private static readonly object _lock = new();
    private static List<Miss>? _misses;
    private static string _current = "";

    public static void Begin()
    {
        lock (_lock)
        {
            _misses = [];
            _current = "";
        }
    }

    /// <summary>Names the patch every following miss belongs to.</summary>
    public static void Entering(string patchName)
    {
        lock (_lock)
        {
            _current = patchName;
            RunLog.Line($"patch: {patchName}");
        }
    }

    public static void AnchorMissing(string file, string anchor) =>
        Add($"{file} no longer contains '{anchor}'");

    public static void FileMissing(string path) => Add($"{path} not found in the game files");

    /// <summary>
    /// A sweep over many files that changed none of them. Reported once for the sweep, since a
    /// single file not carrying the literal is normal and only the empty total is a failure.
    /// </summary>
    public static void NoMatch(string scope, string what) =>
        Add($"no file under {scope} contains {what}");

    private static void Add(string detail)
    {
        lock (_lock)
        {
            _misses?.Add(new Miss(_current, detail));
            // Logged as it happens, not only in the batch End() returns, so a crash mid-run
            // still leaves a trail of what had already gone wrong.
            RunLog.Line($"  miss [{_current}]: {detail}");
        }
    }

    public static List<Miss> End()
    {
        lock (_lock)
        {
            var misses = _misses ?? [];
            _misses = null;
            _current = "";
            return misses;
        }
    }
}
