using LibBundle3.Nodes;
using LibBundle3.Records;

namespace PoeRedux.Patches;

/// <summary>
/// Returns the new content of a file, or <see langword="null"/> to leave it as it is.
/// Must not modify <paramref name="content"/>.
/// </summary>
public delegate byte[]? FileTransform(byte[] content);

/// <summary>
/// Files that the selected patches change, each with its transforms in patch order.
/// </summary>
/// <param name="readOriginals">
/// Reads files the way the transforms will see them. <see cref="Services.PatchRunner"/> passes a reader that
/// returns the backed-up original of a file PoE Redux changed; by default files are read as they are.
/// </param>
public sealed class PatchPlan(Func<IReadOnlyCollection<FileRecord>, IReadOnlyDictionary<FileRecord, byte[]>>? readOriginals = null)
{
    private readonly Dictionary<FileRecord, PlannedFile> _byRecord = [];
    private readonly List<PlannedFile> _files = [];
    private readonly Dictionary<string, object> _shared = new(StringComparer.Ordinal);

    public IReadOnlyList<PlannedFile> Files => _files;

    /// <summary>
    /// The content of files a patch reads to decide what to change, from the same bytes the transforms start from.
    /// </summary>
    public IReadOnlyDictionary<FileRecord, byte[]> ReadOriginals(IReadOnlyCollection<FileRecord> records) =>
        records.Count == 0 ? new Dictionary<FileRecord, byte[]>() : (readOriginals ?? ReadCurrent)(records);

    /// <summary>A value worked out once per apply and shared by the patches that need it.</summary>
    public T Shared<T>(string key, Func<T> create) where T : class
    {
        if (!_shared.TryGetValue(key, out var value))
            _shared[key] = value = create();
        return (T)value;
    }

    /// <summary>Reads files as they are, grouped by bundle.</summary>
    public static Dictionary<FileRecord, byte[]> ReadCurrent(IReadOnlyCollection<FileRecord> records)
    {
        var result = new Dictionary<FileRecord, byte[]>(records.Count);
        var gate = new object();
        LibBundle3.Index.Extract(records, (record, content) =>
        {
            var bytes = content?.ToArray() ?? throw new FileNotFoundException("Failed to get bundle: " + record.BundleRecord.Path);
            lock (gate)
                result[record] = bytes;
            return false;
        });
        return result;
    }

    /// <summary>
    /// Number of transforms added so far, over all files. A patch that adds none found no files to work on.
    /// </summary>
    public int TransformCount { get; private set; }

    public void Add(FileNode file, FileTransform transform) => Add(file.Record, transform);

    public void Add(FileRecord record, FileTransform transform)
    {
        if (!_byRecord.TryGetValue(record, out var planned))
        {
            planned = new PlannedFile(record);
            _byRecord.Add(record, planned);
            _files.Add(planned);
        }
        planned.Transforms.Add(transform);
        TransformCount++;
    }
}

public sealed class PlannedFile(FileRecord record)
{
    public FileRecord Record { get; } = record;
    public List<FileTransform> Transforms { get; } = [];

    /// <summary>
    /// Runs the transforms in order, each on the output of the previous one.
    /// </summary>
    /// <returns>The final content, or <see langword="null"/> if it is byte-for-byte the same as <paramref name="original"/>.</returns>
    public byte[]? Apply(byte[] original)
    {
        var current = original;
        foreach (var transform in Transforms)
            current = transform(current) ?? current;
        return current.AsSpan().SequenceEqual(original) ? null : current;
    }
}
