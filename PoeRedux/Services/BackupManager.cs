using LibBundle3.Records;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PoeRedux.Services;

public enum PoeGame
{
    PoE1,
    PoE2,
}

public static class BackupManager
{
    /// <summary>
    /// Hash of the bytes this tool wrote for each backed up path, one "hash path" line. A file
    /// whose current bytes no longer match its line was replaced by something else, a game update
    /// above all, which makes the original stored beside it a previous version's file and unsafe
    /// to write back. Never a game path, so it stays out of counts and out of the restore.
    /// </summary>
    private const string ManifestEntry = "_poeredux_written.txt";

    private static readonly object _lock = new();
    private static ZipArchive? _zip;
    private static HashSet<string>? _knownPaths;
    private static VanillaSource? _vanillaSource;
    private static int _vanillaFallbacks;

    /// <summary>
    /// The originals a newly begun backup should read from, when a path is not already known.
    /// Null means captures fall back to whatever the local install currently holds, exactly as
    /// before this existed, since a source unreachable at <see cref="Begin"/> time is not fatal.
    /// </summary>
    public static void SetVanillaSource(VanillaSource? source) => _vanillaSource = source;

    /// <summary>
    /// How many newly captured originals fell back to the local install because the vanilla
    /// source had no such path, or none was set. Zero with a source set means every capture this
    /// run was verifiably the CDN's own bytes.
    /// </summary>
    public static int VanillaFallbackCount => _vanillaFallbacks;

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

    /// <summary>Every path and original bytes currently held in the backup, for a change review.</summary>
    public static IEnumerable<(string Path, byte[] Original)> EnumerateBackedUp(PoeGame game)
    {
        if (!HasBackup(game)) yield break;

        using var zip = ZipFile.OpenRead(GetBackupFilePath(game));
        foreach (var entry in zip.Entries.Where(IsGameFile))
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            yield return (entry.FullName, ms.ToArray());
        }
    }

    public static int CountBackedUpFiles(PoeGame game)
    {
        if (!HasBackup(game)) return 0;
        try
        {
            using var zip = ZipFile.OpenRead(GetBackupFilePath(game));
            return zip.Entries.Count(IsGameFile);
        }
        catch { return 0; }
    }

    private static bool IsGameFile(ZipArchiveEntry entry) =>
        !entry.FullName.EndsWith('/') && entry.FullName != ManifestEntry;

    private static string HashOf(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>
    /// Path to the hash of what this tool last wrote there. Empty for a backup taken before
    /// manifests existed, which leaves every file unverifiable rather than stale.
    /// </summary>
    private static Dictionary<string, string> ReadManifest(PoeGame game)
    {
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!HasBackup(game)) return manifest;
        try
        {
            using var zip = ZipFile.OpenRead(GetBackupFilePath(game));
            if (zip.GetEntry(ManifestEntry) is not ZipArchiveEntry entry) return manifest;

            using var reader = new StreamReader(entry.Open());
            while (reader.ReadLine() is string line)
            {
                var split = line.IndexOf(' ');
                if (split > 0)
                    manifest[line[(split + 1)..]] = line[..split];
            }
        }
        catch { manifest.Clear(); }
        return manifest;
    }

    /// <summary>
    /// Records what the patches just wrote, so a later restore can tell its own output from a
    /// file the game has replaced since. Call after patching and before <see cref="End"/>.
    /// </summary>
    public static void Seal(LibBundle3.Index index)
    {
        lock (_lock)
        {
            if (_zip == null || _knownPaths == null) return;

            var lines = new List<string>();
            foreach (var path in _knownPaths)
            {
                if (path == ManifestEntry) continue;
                if (index.TryGetFile(path, out var file))
                    lines.Add($"{HashOf(file.Read().Span)} {path}");
            }

            _zip.GetEntry(ManifestEntry)?.Delete();
            using var stream = _zip.CreateEntry(ManifestEntry, CompressionLevel.Optimal).Open();
            using var writer = new StreamWriter(stream);
            foreach (var line in lines)
                writer.WriteLine(line);
        }
    }

    public static void Begin(PoeGame game)
    {
        lock (_lock)
        {
            // End() clears the vanilla source along with the zip; SetVanillaSource is always
            // called before Begin(), so it has to survive this internal cleanup or every backup
            // silently falls back to the local install.
            var vanillaSource = _vanillaSource;
            End();
            _vanillaSource = vanillaSource;

            var path = GetBackupFilePath(game);
            _knownPaths = new HashSet<string>(StringComparer.Ordinal);
            _vanillaFallbacks = 0;

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

            byte[] originalBytes;
            if (_vanillaSource is not null && _vanillaSource.TryRead(path, out var vanillaBytes))
            {
                originalBytes = vanillaBytes;
                RunLog.Line($"  backup {path}: CDN vanilla");
            }
            else
            {
                originalBytes = record.Read().ToArray();
                if (_vanillaSource is not null) _vanillaFallbacks++;
                RunLog.Line($"  backup {path}: local install"
                    + (_vanillaSource is null ? " (no vanilla source)" : " (not in vanilla index)"));
            }

            var entry = _zip.CreateEntry(path, CompressionLevel.Optimal);
            using var es = entry.Open();
            es.Write(originalBytes);
        }
    }

    public static void End()
    {
        lock (_lock)
        {
            _zip?.Dispose();
            _zip = null;
            _knownPaths = null;
            // Owned by the caller of SetVanillaSource, which disposes it; just drop the reference
            // so a later Begin() with no source set does not reuse a stale one.
            _vanillaSource = null;
        }
    }

    /// <param name="Restored">Files written back to their original bytes.</param>
    /// <param name="Skipped">
    /// Files something else has written since this tool patched them, a game update above all.
    /// Their stored original belongs to the previous version, so writing it back would leave the
    /// install carrying two versions at once.
    /// </param>
    public sealed record RestoreResult(int Restored, int Skipped);

    public static RestoreResult Restore(LibBundle3.Index index, PoeGame game, Action<int, int>? progress = null)
    {
        var path = GetBackupFilePath(game);
        if (!File.Exists(path)) return new(0, 0);

        var manifest = ReadManifest(game);

        RunLog.Line($"restore: {GetBackupFilePath(game)}");

        using var zip = ZipFile.OpenRead(path);
        // The manifest is not a game path, and Index.Replace throws on an entry it cannot find.
        var entries = new List<ZipArchiveEntry>();
        var skipped = 0;
        foreach (var entry in zip.Entries.Where(IsGameFile))
        {
            if (manifest.TryGetValue(entry.FullName, out var written)
                && index.TryGetFile(entry.FullName, out var current)
                && HashOf(current.Read().Span) != written)
            {
                skipped++;
                RunLog.Line($"  skip {entry.FullName}: written since this tool's last apply (game update, most likely)");
                continue;
            }
            entries.Add(entry);
        }
        var total = entries.Count;

        int done = 0;
        var restored = LibBundle3.Index.Replace(index, entries, (record, path) =>
        {
            done++;
            RunLog.Line($"  restored {path}");
            progress?.Invoke(done, total);
            return false; // continue
        }, saveIndex: false);
        RunLog.Line($"restore done: {restored} restored, {skipped} skipped");
        return new(restored, skipped);
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
