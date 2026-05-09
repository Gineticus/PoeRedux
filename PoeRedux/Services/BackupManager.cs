using LibBundle3;
using LibBundle3.Records;
using System.IO;
using System.Text;

namespace PoeRedux.Services;

public enum PoeGame
{
    PoE1,
    PoE2,
}

/// <summary>
/// Captures original file bytes before patches overwrite them, and restores them on
/// demand. Crash-safe streaming format: each entry is appended immediately, so a
/// mid-patch crash still leaves a recoverable partial backup.
///
/// Usage:
///   BackupManager.Begin(game);
///   try {
///       // patches call BackupManager.RecordOriginal(record) before record.Write(...)
///   } finally {
///       BackupManager.End();
///   }
///
///   // later:
///   BackupManager.Restore(index, game);
///   index.Save();
///   BackupManager.DeleteBackup(game);
/// </summary>
public static class BackupManager
{
    private const uint MAGIC = 0x4B425350; // "PSBK"
    private const int VERSION = 1;

    private static readonly object _lock = new();
    private static FileStream? _stream;
    private static HashSet<string>? _knownPaths;

    public static string GetBackupFilePath(PoeGame game)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PoeRedux", "Backups");
        Directory.CreateDirectory(dir);
        var name = game == PoeGame.PoE2 ? "poe2.bak" : "poe1.bak";
        return Path.Combine(dir, name);
    }

    public static bool HasBackup(PoeGame game) => File.Exists(GetBackupFilePath(game));

    public static int CountBackedUpFiles(PoeGame game)
    {
        if (!HasBackup(game)) return 0;
        try { return ReadAllEntries(GetBackupFilePath(game)).Count; }
        catch { return 0; }
    }

    /// <summary>Open a session. Existing entries (from prior sessions) are preserved
    /// and their paths are skipped — so the very first original we ever saw stays as
    /// truth even if patches are re-applied without restoring first.</summary>
    public static void Begin(PoeGame game)
    {
        lock (_lock)
        {
            End();

            var path = GetBackupFilePath(game);
            _knownPaths = new HashSet<string>(StringComparer.Ordinal);

            if (File.Exists(path))
            {
                try
                {
                    foreach (var entry in ReadAllEntries(path))
                        _knownPaths.Add(entry.Path);
                }
                catch
                {
                    File.Delete(path);
                    _knownPaths.Clear();
                }
            }

            var newFile = !File.Exists(path);
            _stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            if (newFile)
            {
                using var bw = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
                bw.Write(MAGIC);
                bw.Write(VERSION);
                bw.Write((int)game);
                bw.Flush();
            }
            _stream.Seek(0, SeekOrigin.End);
        }
    }

    /// <summary>Capture this file's current bytes as "original" before a patch
    /// overwrites it. Patches call this immediately before <c>record.Write(...)</c>.
    /// If the path is already in the backup, this is a fast no-op.</summary>
    public static void RecordOriginal(FileRecord record)
    {
        if (record == null) return;

        lock (_lock)
        {
            if (_stream == null || _knownPaths == null) return;

            var path = record.Path ?? string.Empty;
            if (string.IsNullOrEmpty(path)) return;
            if (!_knownPaths.Add(path)) return;

            var originalBytes = record.Read();

            var pathBytes = Encoding.UTF8.GetBytes(path);
            using var bw = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            bw.Write(pathBytes.Length);
            bw.Write(pathBytes);
            bw.Write(originalBytes.Length);
            bw.Write(originalBytes.Span);
            bw.Flush();
        }
    }

    public static void End()
    {
        lock (_lock)
        {
            _stream?.Dispose();
            _stream = null;
            _knownPaths = null;
        }
    }

    /// <summary>Apply backed-up originals back into the index. Caller is responsible
    /// for opening the index, calling <c>index.Save()</c>, and disposing.</summary>
    public static int Restore(LibBundle3.Index index, PoeGame game, Action<int, int, string>? progress = null)
    {
        var path = GetBackupFilePath(game);
        if (!File.Exists(path)) return 0;

        var entries = ReadAllEntries(path);
        int done = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            progress?.Invoke(i + 1, entries.Count, entry.Path);
            if (index.TryGetFile(entry.Path, out var record))
            {
                record.Write(entry.Bytes, false);
                done++;
            }
        }
        return done;
    }

    public static void DeleteBackup(PoeGame game)
    {
        var path = GetBackupFilePath(game);
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    public record BackupEntry(string Path, byte[] Bytes);

    private static List<BackupEntry> ReadAllEntries(string filePath)
    {
        var list = new List<BackupEntry>();
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

        if (fs.Length < 12) return list;

        var magic = br.ReadUInt32();
        if (magic != MAGIC) throw new InvalidDataException("Not a PoeRedux backup file.");
        var version = br.ReadInt32();
        if (version != VERSION) throw new InvalidDataException($"Unsupported backup version: {version}");
        _ = br.ReadInt32();

        while (fs.Position < fs.Length)
        {
            try
            {
                int pathLen = br.ReadInt32();
                if (pathLen <= 0 || pathLen > 4096) break;
                var pathBytes = br.ReadBytes(pathLen);
                if (pathBytes.Length != pathLen) break;
                int dataLen = br.ReadInt32();
                if (dataLen < 0 || fs.Position + dataLen > fs.Length) break;
                var data = br.ReadBytes(dataLen);
                if (data.Length != dataLen) break;
                list.Add(new BackupEntry(Encoding.UTF8.GetString(pathBytes), data));
            }
            catch (EndOfStreamException) { break; }
        }
        return list;
    }
}
