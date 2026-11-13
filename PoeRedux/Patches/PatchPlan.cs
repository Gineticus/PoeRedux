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
public sealed class PatchPlan
{
    private readonly Dictionary<FileRecord, PlannedFile> _byRecord = [];
    private readonly List<PlannedFile> _files = [];

    public IReadOnlyList<PlannedFile> Files => _files;

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
