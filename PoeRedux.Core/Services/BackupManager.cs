using PoeRedux.Models;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace PoeRedux.Services;

/// <summary>
/// Original bytes of the game files PoeRedux changes, kept per game as zip archives in
/// <see cref="BackupDirectory"/>: poe1.zip, poe1.2.zip, poe1.3.zip, and so on. An apply that changes
/// files without a backup writes one new archive, so each path is stored once and that copy is the original.
/// </summary>
public static class BackupManager
{
    public static string BackupDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "Backups");

    private static string Prefix(PoeGame game) => game == PoeGame.PoE2 ? "poe2" : "poe1";

    /// <summary>
    /// Backup archives of <paramref name="game"/>, oldest first.
    /// </summary>
    public static List<string> GetArchives(PoeGame game)
    {
        var prefix = Prefix(game);
        var archives = new List<(int Number, string Path)>();
        if (Directory.Exists(BackupDirectory))
        {
            foreach (var path in Directory.GetFiles(BackupDirectory, prefix + ".*"))
            {
                var number = GetArchiveNumber(Path.GetFileName(path), prefix);
                if (number > 0)
                    archives.Add((number, path));
            }
        }
        archives.Sort((a, b) => a.Number.CompareTo(b.Number));
        return archives.ConvertAll(a => a.Path);
    }

    // poe1.zip -> 1, poe1.7.zip -> 7, anything else -> 0
    private static int GetArchiveNumber(string fileName, string prefix)
    {
        if (fileName.Equals(prefix + ".zip", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (!fileName.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return 0;
        var number = fileName.AsSpan(prefix.Length + 1, fileName.Length - prefix.Length - 1 - ".zip".Length);
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 1 ? n : 0;
    }

    public static bool HasBackup(PoeGame game) => GetArchives(game).Count > 0;

    public static int CountBackedUpFiles(PoeGame game)
    {
        var count = 0;
        foreach (var path in GetArchives(game))
        {
            try
            {
                using var zip = ZipFile.OpenRead(path);
                count += zip.Entries.Count(e => !e.FullName.EndsWith('/'));
            }
            catch { /* unreadable archive */ }
        }
        return count;
    }

    /// <summary>
    /// Starts recording originals for one apply. Paths stored in earlier archives are skipped.
    /// </summary>
    public static BackupSession Begin(PoeGame game)
    {
        Directory.CreateDirectory(BackupDirectory);
        var prefix = Prefix(game);

        // Left over from an apply that was interrupted before it finished.
        foreach (var temp in Directory.GetFiles(BackupDirectory, prefix + ".*.zip.tmp"))
        {
            try { File.Delete(temp); } catch { /* best effort */ }
        }

        var existing = new HashSet<string>(StringComparer.Ordinal);
        var last = 0;
        foreach (var path in GetArchives(game))
        {
            last = GetArchiveNumber(Path.GetFileName(path), prefix);
            try
            {
                using var zip = ZipFile.OpenRead(path);
                foreach (var entry in zip.Entries)
                    existing.Add(entry.FullName);
            }
            catch
            {
                // Corrupt/unreadable archive: set it aside and continue without it.
                try { File.Move(path, path + ".broken", true); } catch { /* best effort */ }
            }
        }

        var next = last + 1;
        var archive = Path.Combine(BackupDirectory, next == 1 ? prefix + ".zip" : $"{prefix}.{next}.zip");
        return new BackupSession(existing, archive);
    }

    /// <summary>
    /// Writes the backed-up originals of <paramref name="game"/> back to the game files and saves the index.
    /// Files are found by path hash, so the index does not need <see cref="LibBundle3.Index.ParsePaths"/>.
    /// </summary>
    /// <returns>Number of files restored.</returns>
    public static int Restore(LibBundle3.Index index, PoeGame game, IProgress<(int Done, int Total)>? progress = null)
    {
        var archives = new List<ZipArchive>();
        try
        {
            foreach (var path in GetArchives(game))
                archives.Add(ZipFile.OpenRead(path));

            var total = archives.Sum(zip => zip.Entries.Count);
            var restored = new HashSet<string>(StringComparer.Ordinal);
            int done = 0, count = 0;
            foreach (var zip in archives)
            {
                // Each path is normally stored once; if not, the oldest archive holds the original.
                var entries = zip.Entries.Where(e => !e.FullName.EndsWith('/') && restored.Add(e.FullName));
                count += LibBundle3.Index.Replace(index, entries, (record, path) =>
                {
                    done++;
                    progress?.Report((done, total));
                    return false; // continue
                }, saveIndex: false);
            }
            index.Save();
            return count;
        }
        finally
        {
            foreach (var zip in archives)
                zip.Dispose();
        }
    }

    public static void DeleteBackup(PoeGame game)
    {
        if (!Directory.Exists(BackupDirectory))
            return;
        var prefix = Prefix(game);
        foreach (var path in Directory.GetFiles(BackupDirectory, prefix + ".*"))
        {
            var name = Path.GetFileName(path);
            if (GetArchiveNumber(name, prefix) > 0
                || name.EndsWith(".zip.broken", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".zip.tmp", StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { /* best effort */ }
            }
        }
    }
}

/// <summary>
/// Originals recorded during one apply. They are written straight to a temporary file and become a
/// backup archive on <see cref="Commit"/>; disposing without committing discards them.
/// </summary>
public sealed class BackupSession : IDisposable
{
    private readonly HashSet<string> _existing;
    private readonly HashSet<string> _added = new(StringComparer.Ordinal);
    private readonly string _archivePath;
    private readonly string _tempPath;
    private FileStream? _stream;
    private ZipArchive? _zip;
    private bool _finished;

    internal BackupSession(HashSet<string> existing, string archivePath)
    {
        _existing = existing;
        _archivePath = archivePath;
        _tempPath = archivePath + ".tmp";
    }

    /// <summary>
    /// Number of originals recorded in this session.
    /// </summary>
    public int Count => _added.Count;

    /// <summary>
    /// Whether <paramref name="path"/> has no backup from an earlier apply. Thread-safe.
    /// </summary>
    public bool NeedsBackup(string path) => !string.IsNullOrEmpty(path) && !_existing.Contains(path);

    /// <summary>
    /// Stores the original bytes of <paramref name="path"/>. Not thread-safe.
    /// </summary>
    public void Add(string path, ReadOnlySpan<byte> original)
    {
        if (!NeedsBackup(path) || !_added.Add(path))
            return;

        if (_zip is null)
        {
            _stream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            _zip = new ZipArchive(_stream, ZipArchiveMode.Create);
        }
        var entry = _zip.CreateEntry(path, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        entryStream.Write(original);
    }

    /// <summary>
    /// Finishes the archive and gives it its final name.
    /// </summary>
    public void Commit()
    {
        if (_finished)
            return;
        if (_zip is not null)
        {
            _zip.Dispose();
            _stream!.Dispose();
            _zip = null;
            _stream = null;
            File.Move(_tempPath, _archivePath);
        }
        _finished = true;
    }

    public void Dispose()
    {
        if (_finished)
            return;
        _finished = true;
        _zip?.Dispose();
        _stream?.Dispose();
        _zip = null;
        _stream = null;
        try { File.Delete(_tempPath); } catch { /* best effort */ }
    }
}
