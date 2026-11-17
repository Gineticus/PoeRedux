using PoeRedux.Models;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoeRedux.Services;

/// <summary>
/// What PoE Redux knows about one game file it changed.
/// </summary>
public sealed class BackupEntry
{
    /// <summary>Archive file name (poe2.zip, poe2.3.zip) that holds the original bytes.</summary>
    public required string Archive { get; set; }

    /// <summary>SHA-256 of the original bytes, or null for a backup made before hashes were kept.</summary>
    public string? Original { get; set; }

    /// <summary>
    /// SHA-256 of the bytes PoE Redux wrote last, and of the write before it. Empty when the file holds its
    /// original bytes. A file whose bytes match none of these was replaced by someone else, a game update
    /// above all, and its stored original belongs to the old game version.
    /// </summary>
    public List<string> Written { get; set; } = [];

    /// <summary>True for an entry read from an archive written before hashes were kept.</summary>
    [JsonIgnore]
    public bool Legacy => Original is null;

    /// <summary>Whether the game file holds bytes PoE Redux wrote, as far as this entry knows.</summary>
    [JsonIgnore]
    public bool IsPatched => Legacy || Written.Count > 0;

    public bool Wrote(string hash) => Written.Contains(hash, StringComparer.Ordinal);
}

/// <summary>
/// Original bytes of the game files PoE Redux changes, kept per game in <see cref="BackupDirectory"/>:
/// archives poe1.zip, poe1.2.zip, ... with the bytes, and poe1.backup.json with one <see cref="BackupEntry"/>
/// per file. An apply that changes files without a backup writes one new archive. When the game replaces a
/// file, the next apply backs up the new original in a new archive and the entry points there.
/// </summary>
public sealed class BackupStore
{
    private const int ManifestVersion = 1;
    private const int KeptWrites = 2;

    public static string BackupDirectory { get; set; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "Backups");

    private readonly string _prefix;
    private readonly Dictionary<string, BackupEntry> _entries;
    private readonly Dictionary<string, ZipArchive> _openArchives = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _readLock = new();

    private BackupStore(PoeGame game, Dictionary<string, BackupEntry> entries, List<string> archives)
    {
        Game = game;
        _prefix = Prefix(game);
        _entries = entries;
        Archives = archives;
    }

    public PoeGame Game { get; }

    /// <summary>Archive paths, oldest first.</summary>
    public List<string> Archives { get; }

    public IReadOnlyDictionary<string, BackupEntry> Entries => _entries;

    /// <summary>Files that hold bytes PoE Redux wrote, as far as the backup knows.</summary>
    public int PatchedCount => _entries.Values.Count(e => e.IsPatched);

    private static string Prefix(PoeGame game) => game == PoeGame.PoE2 ? "poe2" : "poe1";

    private static string ManifestPath(string prefix) => Path.Combine(BackupDirectory, prefix + ".backup.json");

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>Reads the backup of <paramref name="game"/>. Archives that cannot be opened are set aside as .broken.</summary>
    public static BackupStore Open(PoeGame game)
    {
        var prefix = Prefix(game);
        var archives = GetArchives(game);
        var entries = new Dictionary<string, BackupEntry>(StringComparer.Ordinal);

        var manifestPath = ManifestPath(prefix);
        if (File.Exists(manifestPath))
        {
            var manifest = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), BackupJson.Default.Manifest)
                ?? throw new InvalidDataException("The backup list is empty: " + manifestPath);
            var names = archives.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, entry) in manifest.Files)
            {
                // An entry whose archive is gone cannot be restored; drop it rather than fail later.
                if (names.Contains(entry.Archive))
                    entries[path] = entry;
            }
            return new BackupStore(game, entries, archives);
        }

        // Archives from before the backup list existed: every path is an original of unknown age. The oldest
        // archive that has a path holds its original.
        foreach (var archive in archives.ToList())
        {
            try
            {
                using var zip = ZipFile.OpenRead(archive);
                foreach (var e in zip.Entries)
                {
                    if (!e.FullName.EndsWith('/'))
                        entries.TryAdd(e.FullName, new BackupEntry { Archive = Path.GetFileName(archive) });
                }
            }
            catch (InvalidDataException)
            {
                SetAside(archive);
                archives.Remove(archive);
            }
        }
        return new BackupStore(game, entries, archives);
    }

    private static void SetAside(string archive)
    {
        try { File.Move(archive, archive + ".broken", true); } catch (IOException) { /* best effort */ }
    }

    /// <summary>Backup archives of <paramref name="game"/>, oldest first.</summary>
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

    public BackupEntry? Find(string path) => _entries.GetValueOrDefault(path);

    /// <summary>Original bytes of <paramref name="path"/>. Thread-safe.</summary>
    public byte[] ReadOriginal(string path)
    {
        var entry = _entries[path];
        lock (_readLock)
        {
            if (!_openArchives.TryGetValue(entry.Archive, out var zip))
            {
                zip = ZipFile.OpenRead(Path.Combine(BackupDirectory, entry.Archive));
                _openArchives[entry.Archive] = zip;
            }
            var zipEntry = zip.GetEntry(path) ?? throw new InvalidDataException($"{entry.Archive} has no copy of {path}.");
            using var stream = zipEntry.Open();
            using var memory = new MemoryStream(checked((int)zipEntry.Length));
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }

    /// <summary>Closes the archives opened by <see cref="ReadOriginal"/>.</summary>
    public void CloseArchives()
    {
        lock (_readLock)
        {
            foreach (var zip in _openArchives.Values)
                zip.Dispose();
            _openArchives.Clear();
        }
    }

    /// <summary>Starts recording new originals in the next numbered archive.</summary>
    public BackupSession BeginSession()
    {
        Directory.CreateDirectory(BackupDirectory);
        // Left over from an apply that was interrupted before it finished.
        foreach (var temp in Directory.GetFiles(BackupDirectory, _prefix + ".*.zip.tmp"))
        {
            try { File.Delete(temp); } catch (IOException) { /* best effort */ }
        }
        var last = Archives.Count == 0 ? 0 : GetArchiveNumber(Path.GetFileName(Archives[^1]), _prefix);
        var next = last + 1;
        var archive = Path.Combine(BackupDirectory, next == 1 ? _prefix + ".zip" : $"{_prefix}.{next}.zip");
        return new BackupSession(archive);
    }

    /// <summary>Records that <paramref name="path"/> now holds bytes with hash <paramref name="written"/>.</summary>
    public void RecordWrite(string path, BackupEntry entry, string written)
    {
        entry.Written.Remove(written);
        entry.Written.Insert(0, written);
        if (entry.Written.Count > KeptWrites)
            entry.Written.RemoveRange(KeptWrites, entry.Written.Count - KeptWrites);
        _entries[path] = entry;
    }

    /// <summary>Records that <paramref name="path"/> holds its original bytes, with hash <paramref name="original"/>, again.</summary>
    public void RecordOriginal(string path, string original)
    {
        if (_entries.GetValueOrDefault(path) is not { } entry)
            return;
        entry.Written.Clear();
        entry.Original ??= original;
    }

    /// <summary>Drops the hash of the write before the last one; called once the index with the last write is saved.</summary>
    public void KeepLatestWrites()
    {
        foreach (var entry in _entries.Values)
        {
            if (entry.Written.Count > 1)
                entry.Written.RemoveRange(1, entry.Written.Count - 1);
        }
    }

    /// <summary>Forgets <paramref name="path"/>: the game replaced it, so its stored original is out of date.</summary>
    public void Forget(string path) => _entries.Remove(path);

    public void AddArchive(string archivePath)
    {
        if (!Archives.Contains(archivePath, StringComparer.OrdinalIgnoreCase))
            Archives.Add(archivePath);
    }

    /// <summary>
    /// Writes the backup list. Legacy entries are written with an empty original hash so the next run still
    /// treats them as legacy. Archives that no entry needs any more are deleted.
    /// </summary>
    public void Save()
    {
        Directory.CreateDirectory(BackupDirectory);
        var manifest = new BackupManifest
        {
            Version = ManifestVersion,
            Files = new SortedDictionary<string, BackupEntry>(_entries, StringComparer.Ordinal),
        };
        var path = ManifestPath(_prefix);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, BackupJson.Default.Manifest));
        File.Move(temp, path, true);

        CloseArchives();
        var used = _entries.Values.Select(e => e.Archive).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var archive in Archives.ToList())
        {
            if (used.Contains(Path.GetFileName(archive)))
                continue;
            try
            {
                File.Delete(archive);
                Archives.Remove(archive);
            }
            catch (IOException) { /* deleted next time */ }
        }
    }

    /// <summary>Deletes every archive and the backup list of <paramref name="game"/>.</summary>
    public static void Delete(PoeGame game)
    {
        if (!Directory.Exists(BackupDirectory))
            return;
        var prefix = Prefix(game);
        foreach (var path in Directory.GetFiles(BackupDirectory, prefix + ".*"))
        {
            var name = Path.GetFileName(path);
            if (GetArchiveNumber(name, prefix) > 0
                || name.Equals(prefix + ".backup.json", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".zip.broken", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".zip.tmp", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".backup.json.tmp", StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch (IOException) { /* best effort */ }
            }
        }
    }
}

internal sealed class BackupManifest
{
    public int Version { get; set; }
    public SortedDictionary<string, BackupEntry> Files { get; set; } = new(StringComparer.Ordinal);
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BackupManifest), TypeInfoPropertyName = "Manifest")]
internal sealed partial class BackupJson : JsonSerializerContext;

/// <summary>
/// Originals recorded during one apply. They are written straight to a temporary file and become a
/// backup archive on <see cref="Commit"/>; disposing without committing discards them.
/// </summary>
public sealed class BackupSession : IDisposable
{
    private readonly HashSet<string> _added = new(StringComparer.Ordinal);
    private readonly string _tempPath;
    private FileStream? _stream;
    private ZipArchive? _zip;
    private bool _finished;

    internal BackupSession(string archivePath)
    {
        ArchivePath = archivePath;
        _tempPath = archivePath + ".tmp";
    }

    public string ArchivePath { get; }
    public string ArchiveName => Path.GetFileName(ArchivePath);

    /// <summary>Number of originals recorded in this session.</summary>
    public int Count => _added.Count;

    /// <summary>Stores the original bytes of <paramref name="path"/>. Not thread-safe.</summary>
    public void Add(string path, ReadOnlySpan<byte> original)
    {
        if (!_added.Add(path))
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

    /// <summary>Finishes the archive and gives it its final name.</summary>
    /// <returns>Whether an archive was written.</returns>
    public bool Commit()
    {
        if (_finished)
            return false;
        _finished = true;
        if (_zip is null)
            return false;
        _zip.Dispose();
        _stream!.Dispose();
        _zip = null;
        _stream = null;
        File.Move(_tempPath, ArchivePath);
        return true;
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
        try { File.Delete(_tempPath); } catch (IOException) { /* best effort */ }
    }
}
