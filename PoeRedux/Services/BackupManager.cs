using LibBundle3.Records;
using System.IO;
using System.IO.Compression;

namespace PoeRedux.Services;

public enum PoeGame
{
    PoE1,
    PoE2,
}

public static class BackupManager
{
    private static readonly object _lock = new();
    private static ZipArchive? _zip;
    private static HashSet<string>? _knownPaths;

    public static string GetBackupFilePath(PoeGame game)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoeRedux", "Backups");
        Directory.CreateDirectory(dir);
        var name = game == PoeGame.PoE2 ? "poe2.zip" : "poe1.zip";
        return Path.Combine(dir, name);
    }

    public static bool HasBackup(PoeGame game) => File.Exists(GetBackupFilePath(game));

    public static int CountBackedUpFiles(PoeGame game)
    {
        if (!HasBackup(game)) return 0;
        try
        {
            using var zip = ZipFile.OpenRead(GetBackupFilePath(game));
            return zip.Entries.Count(e => !e.FullName.EndsWith('/'));
        }
        catch { return 0; }
    }

    public static void Begin(PoeGame game)
    {
        lock (_lock)
        {
            End();

            var path = GetBackupFilePath(game);
            _knownPaths = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                _zip = ZipFile.Open(path, ZipArchiveMode.Update);
                foreach (var entry in _zip.Entries)
                    _knownPaths.Add(entry.FullName);
            }
            catch
            {
                // Corrupt/unreadable archive: start fresh.
                _zip?.Dispose();
                _zip = null;
                try { File.Delete(path); } catch { /* best effort */ }
                _knownPaths.Clear();
                _zip = ZipFile.Open(path, ZipArchiveMode.Update);
            }
        }
    }

    public static void RecordOriginal(FileRecord record)
    {
        if (record == null) return;

        lock (_lock)
        {
            if (_zip == null || _knownPaths == null) return;

            var path = record.Path ?? string.Empty;
            if (string.IsNullOrEmpty(path)) return;
            if (!_knownPaths.Add(path)) return;

            var originalBytes = record.Read();

            var entry = _zip.CreateEntry(path, CompressionLevel.Optimal);
            using var es = entry.Open();
            es.Write(originalBytes.Span);
        }
    }

    public static void End()
    {
        lock (_lock)
        {
            _zip?.Dispose();
            _zip = null;
            _knownPaths = null;
        }
    }

    public static int Restore(LibBundle3.Index index, PoeGame game, Action<int, int>? progress = null)
    {
        var path = GetBackupFilePath(game);
        if (!File.Exists(path)) return 0;

        using var zip = ZipFile.OpenRead(path);
        var total = zip.Entries.Count;

        int done = 0;
        return LibBundle3.Index.Replace(index, zip.Entries, (record, path) =>
        {
            done++;
            progress?.Invoke(done, total);
            return false; // continue
        }, saveIndex: false);
    }

    public static void DeleteBackup(PoeGame game)
    {
        var path = GetBackupFilePath(game);
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }
}
