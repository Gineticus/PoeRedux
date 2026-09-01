using System.IO;

namespace PoeRedux.Services;

/// <summary>
/// A plain-text trace of one Apply or Restore run, so a failure is "paste the log" instead of
/// "re-run by hand and read the code". BackupManager and PatchReport call in at the points they
/// already visit every file, so this is additive to their existing walks, not a second pass.
/// </summary>
public static class RunLog
{
    private const int RetainedRuns = 20;

    private static readonly object _lock = new();
    private static StreamWriter? _writer;
    private static string? _path;

    public static string? CurrentPath => _path;

    /// <summary>Test-only seam: real usage always defaults to the real log directory.</summary>
    public static string? DirectoryOverride { get; set; }

    public static string LogDirectory() => DirectoryOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "Logs");

    public static void Begin(string kind, PoeGame game)
    {
        lock (_lock)
        {
            End();

            var dir = LogDirectory();
            Directory.CreateDirectory(dir);
            var name = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{kind}_{game}.log";
            _path = Path.Combine(dir, name);
            _writer = new StreamWriter(_path, append: false) { AutoFlush = true };
            _writer.WriteLine($"{kind} started {DateTime.Now:O}");
            _writer.WriteLine($"game: {game}");
        }
    }

    public static void Line(string text)
    {
        lock (_lock)
            _writer?.WriteLine(text);
    }

    public static void Exception(Exception ex)
    {
        lock (_lock)
        {
            _writer?.WriteLine("EXCEPTION: " + ex);
        }
    }

    public static void End()
    {
        lock (_lock)
        {
            if (_writer is not null)
            {
                _writer.WriteLine($"ended {DateTime.Now:O}");
                _writer.Dispose();
            }
            _writer = null;
            _path = null;
            Prune();
        }
    }

    /// <summary>
    /// Keeps the newest <see cref="RetainedRuns"/> log files, oldest deleted first. The
    /// timestamp-first file name sorts chronologically as a plain string, so no parsing needed.
    /// </summary>
    private static void Prune()
    {
        var dir = LogDirectory();
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*.log")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(RetainedRuns);
        foreach (var old in files)
        {
            try { File.Delete(old); } catch { /* best effort */ }
        }
    }
}
