using LibBundle3;
using LibBundle3.Records;
using PoeRedux.Models;
using PoeRedux.Patches;
using System.IO;
using System.Runtime.ExceptionServices;

namespace PoeRedux.Services;

public enum PatchStage
{
    Preparing,
    Patching,
    Writing,
    Saving,
}

public readonly record struct PatchProgress(PatchStage Stage, int Done, int Total);

public sealed record PatchResult(int FilesChecked, int FilesChanged, int FilesBackedUp);

/// <summary>
/// Applies patches in one pass over the index. Every targeted file is read once, grouped by bundle,
/// with bundles processed in parallel. All patches for a file run in memory, only files whose bytes
/// change are backed up and written, and the index is saved once at the end.
/// </summary>
public static class PatchRunner
{
    // Bundles store data in 256 KB Oodle chunks. A file that starts in the chunk where the previous
    // file ends (or in the next one) joins the same read range, so each chunk is decompressed once.
    // The cap bounds the memory one worker holds at a time.
    private const int ChunkSize = 256 * 1024;
    private const int MaxRangeSize = 8 * 1024 * 1024;
    private const int ProgressStep = 256;

    public static PatchResult Run(LibBundle3.Index index, IReadOnlyList<IPatch> patches, PoeGame game, IProgress<PatchProgress>? progress = null)
    {
        progress?.Report(new(PatchStage.Preparing, 0, 0));

        var plan = new PatchPlan();
        var root = index.BuildTree(true);
        foreach (var patch in patches)
            patch.Collect(root, plan);

        var files = plan.Files;
        using var backup = BackupManager.Begin(game);
        var results = ReadAndPatch(files, backup, progress);

        var changed = results.Count(content => content is not null);
        var written = 0;
        for (var i = 0; i < files.Count; i++)
        {
            if (results[i] is not { } content)
                continue;
            files[i].Record.Write(content);
            results[i] = null; // the index keeps its own copy until Save
            if (++written % ProgressStep == 0 || written == changed)
                progress?.Report(new(PatchStage.Writing, written, changed));
        }

        if (changed > 0)
        {
            progress?.Report(new(PatchStage.Saving, 0, 0));
            backup.Commit(); // the originals are on disk before the game files change
            index.Save();
        }

        return new(files.Count, changed, backup.Count);
    }

    private static byte[]?[] ReadAndPatch(IReadOnlyList<PlannedFile> files, BackupSession backup, IProgress<PatchProgress>? progress)
    {
        var results = new byte[]?[files.Count];
        var bundles = Enumerable.Range(0, files.Count).GroupBy(i => files[i].Record.BundleRecord).ToList();
        var openLock = new object();
        var backupLock = new object();
        var done = 0;
        progress?.Report(new(PatchStage.Patching, 0, files.Count));

        void PatchBundle(IGrouping<BundleRecord, int> group)
        {
            Bundle bundle;
            lock (openLock) // bundle factories, the GGPK one in particular, are not thread-safe
                bundle = OpenBundle(group.Key);

            using (bundle)
            {
                var items = group.OrderBy(i => files[i].Record.Offset).ToArray();
                for (var start = 0; start < items.Length;)
                {
                    var first = files[items[start]].Record;
                    var rangeStart = first.Offset;
                    var rangeEnd = first.Offset + first.Size;
                    var end = start + 1;
                    for (; end < items.Length; end++)
                    {
                        var next = files[items[end]].Record;
                        var nextEnd = Math.Max(rangeEnd, next.Offset + next.Size);
                        if (next.Offset / ChunkSize > Math.Max(rangeEnd - 1, 0) / ChunkSize + 1 || nextEnd - rangeStart > MaxRangeSize)
                            break;
                        rangeEnd = nextEnd;
                    }

                    var data = bundle.ReadWithoutCache(rangeStart, rangeEnd - rangeStart);
                    for (var k = start; k < end; k++)
                    {
                        var i = items[k];
                        var record = files[i].Record;
                        var original = data.AsSpan(record.Offset - rangeStart, record.Size).ToArray();
                        if (files[i].Apply(original) is { } patched)
                        {
                            if (backup.NeedsBackup(record.Path))
                                lock (backupLock)
                                    backup.Add(record.Path, original);
                            results[i] = patched;
                        }

                        var count = Interlocked.Increment(ref done);
                        if (count % ProgressStep == 0 || count == files.Count)
                            progress?.Report(new(PatchStage.Patching, count, files.Count));
                    }
                    start = end;
                }
            }
        }

        try
        {
            Parallel.ForEach(bundles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, PatchBundle);
        }
        catch (AggregateException e)
        {
            // Report the first real error instead of "One or more errors occurred".
            ExceptionDispatchInfo.Throw(e.InnerExceptions[0]);
        }
        return results;
    }

    private static Bundle OpenBundle(BundleRecord record)
    {
        if (record.TryGetBundle(out var bundle, out var error))
            return bundle;
        if (error is not null)
            ExceptionDispatchInfo.Throw(error);
        throw new FileNotFoundException("Failed to get bundle: " + record.Path);
    }
}
