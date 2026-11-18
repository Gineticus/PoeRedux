using LibBundle3;
using LibBundle3.Records;
using PoeRedux.Models;
using PoeRedux.Patches;
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

/// <param name="FilesChecked">Files read: the ones the selected patches target and the ones an earlier apply changed.</param>
/// <param name="FilesWritten">Files written to the game: patched, or put back to their original.</param>
/// <param name="FilesRestored">Files an earlier apply changed that now hold their original bytes again.</param>
/// <param name="FilesBackedUp">Originals added to the backup.</param>
/// <param name="FilesReplacedByGame">
/// Files an earlier apply changed that the game has replaced since, for example in an update. Their old
/// originals were dropped; a selected patch changed them again from the new version.
/// </param>
/// <param name="PatchesWithoutFiles">Selected patches that found no game files to work on.</param>
public sealed record PatchResult(int FilesChecked, int FilesWritten, int FilesRestored, int FilesBackedUp,
    int FilesReplacedByGame, IReadOnlyList<IPatch> PatchesWithoutFiles);

/// <summary>
/// Makes the game files match the selected patches in one pass over the index.
/// <list type="bullet">
/// <item>A file a selected patch targets is patched from its original: the backup's copy when PoE Redux
/// changed it before, otherwise the file as it is.</item>
/// <item>A file an earlier apply changed that no selected patch targets gets its original back.</item>
/// <item>A file the game replaced after an earlier apply is never overwritten with the old original.</item>
/// </list>
/// Files are read once, grouped by bundle, with bundles processed in parallel; only files whose bytes change
/// are written, and the index is saved once at the end.
/// </summary>
public static class PatchRunner
{
    // Bundles store data in 256 KB Oodle chunks. A file that starts in the chunk where the previous
    // file ends (or in the next one) joins the same read range, so each chunk is decompressed once.
    // The cap bounds the memory one worker holds at a time.
    private const int ChunkSize = 256 * 1024;
    private const int MaxRangeSize = 8 * 1024 * 1024;
    private const int ProgressStep = 256;

    private enum Outcome
    {
        Unchanged,
        Write,
        Restore,
        ReplacedByGame,
    }

    private sealed class Work(string path, FileRecord record, PlannedFile? planned)
    {
        /// <summary>The file's path. Record.Path is null when the index was opened without paths.</summary>
        public string Path { get; } = path;
        public FileRecord Record { get; } = record;
        public PlannedFile? Planned { get; } = planned;
        public Outcome Outcome;
        public byte[]? Content;

        /// <summary>Hash of <see cref="Content"/> when it is written as a patched file.</summary>
        public string? WrittenHash;

        /// <summary>Bytes to back up: the file is about to change and no valid backup holds its original.</summary>
        public byte[]? NewOriginal;

        /// <summary>Hash of the original written back by a restore.</summary>
        public string? RestoredHash;

        /// <summary>The backup entry belongs to a version of the file the game has replaced since.</summary>
        public bool Stale;
    }

    /// <summary>
    /// Writes every original back, except to files the game replaced since PoE Redux changed them, and deletes
    /// the backup. Files are found by path hash, so <paramref name="index"/> needs no parsed paths.
    /// </summary>
    public static PatchResult Restore(LibBundle3.Index index, PoeGame game, IProgress<PatchProgress>? progress = null)
    {
        var result = Run(index, [], game, progress);
        BackupStore.Delete(game);
        return result;
    }

    public static PatchResult Run(LibBundle3.Index index, IReadOnlyList<IPatch> patches, PoeGame game, IProgress<PatchProgress>? progress = null)
    {
        progress?.Report(new(PatchStage.Preparing, 0, 0));

        var store = BackupStore.Open(game);
        try
        {
            var plan = new PatchPlan(records => ReadOriginals(records, store));
            // Restoring alone needs no tree: files are found by path hash, and the index may be opened without paths.
            var root = patches.Count > 0 ? index.BuildTree(true) : null;
            var withoutFiles = new List<IPatch>();
            foreach (var patch in patches)
            {
                var before = plan.TransformCount;
                patch.Collect(root!, plan);
                // Either the other game is selected, or a game update moved or renamed the files.
                if (plan.TransformCount == before)
                    withoutFiles.Add(patch);
            }

            var work = plan.Files.Select(f => new Work(f.Record.Path, f.Record, f)).ToList();
            var planned = work.Select(w => w.Path).ToHashSet(StringComparer.Ordinal);
            foreach (var (path, entry) in store.Entries)
            {
                if (entry.IsPatched && !planned.Contains(path) && index.TryGetFile(path, out var record))
                    work.Add(new Work(path, record, null));
            }

            ReadAndDecide(work, store, progress);

            var toWrite = work.Where(w => w.Outcome is Outcome.Write or Outcome.Restore).ToList();
            using var session = store.BeginSession();
            for (var i = 0; i < toWrite.Count; i++)
            {
                var item = toWrite[i];
                if (item.NewOriginal is { } original)
                    session.Add(item.Path, original);
                item.Record.Write(item.Content!);
                item.Content = null; // the index keeps its own copy until Save
                if ((i + 1) % ProgressStep == 0 || i + 1 == toWrite.Count)
                    progress?.Report(new(PatchStage.Writing, i + 1, toWrite.Count));
            }

            // Order matters if anything fails part way:
            // 1. The new originals are on disk before any game file changes.
            // 2. The backup list knows the new hashes before the index is saved. Each entry keeps the hash of
            //    the write before too, so after a failed save the file is still recognised as PoE Redux's.
            // 3. The index is saved, then the older hashes are dropped.
            if (session.Commit())
                store.AddArchive(session.ArchivePath);
            foreach (var item in work)
                Record(store, item, session.ArchiveName);
            store.Save();

            if (toWrite.Count > 0)
            {
                progress?.Report(new(PatchStage.Saving, 0, 0));
                index.Save();
                store.KeepLatestWrites();
                store.Save();
            }

            return new PatchResult(
                FilesChecked: work.Count,
                FilesWritten: toWrite.Count,
                FilesRestored: work.Count(w => w.Outcome == Outcome.Restore),
                FilesBackedUp: work.Count(w => w.NewOriginal is not null),
                FilesReplacedByGame: work.Count(w => w.Stale),
                PatchesWithoutFiles: withoutFiles);
        }
        finally
        {
            store.CloseArchives();
        }
    }

    /// <summary>
    /// Reads files for a patch that decides from their content, starting from the bytes <see cref="Decide"/>
    /// starts from: the backup's original when the file holds bytes PoE Redux wrote.
    /// </summary>
    private static Dictionary<FileRecord, byte[]> ReadOriginals(IReadOnlyCollection<FileRecord> records, BackupStore store)
    {
        var result = PatchPlan.ReadCurrent(records);
        foreach (var (record, current) in result.ToList())
        {
            var entry = store.Find(record.Path);
            if (entry is not null && (entry.Wrote(BackupStore.Hash(current)) || entry.Legacy && entry.Written.Count == 0))
                result[record] = store.ReadOriginal(record.Path);
        }
        return result;
    }

    // Updates the backup list for one file after the writes.
    private static void Record(BackupStore store, Work item, string newArchive)
    {
        var path = item.Path;
        if (item.Stale && item.NewOriginal is null)
        {
            store.Forget(path);
            return;
        }
        switch (item.Outcome)
        {
            case Outcome.Write:
                var entry = item.NewOriginal is { } original
                    ? new BackupEntry { Archive = newArchive, Original = BackupStore.Hash(original) }
                    : store.Find(path)!;
                store.RecordWrite(path, entry, item.WrittenHash!);
                break;
            case Outcome.Restore:
                store.RecordOriginal(path, item.RestoredHash!);
                break;
        }
    }

    /// <summary>
    /// Decides what happens to one file, given its current bytes. Runs on worker threads and only changes
    /// <paramref name="item"/>; reading originals from the backup is thread-safe.
    /// </summary>
    private static void Decide(Work item, byte[] current, BackupStore store)
    {
        var path = item.Path;
        var entry = store.Find(path);
        var currentHash = BackupStore.Hash(current);

        // source: the bytes the selected patches start from. ours: the file holds bytes PoE Redux wrote.
        byte[] source;
        bool ours;
        if (entry is null)
        {
            source = current;
            ours = false;
        }
        else if (entry.Wrote(currentHash))
        {
            source = store.ReadOriginal(path);
            ours = true;
        }
        else if (entry.Legacy && entry.Written.Count == 0)
        {
            // A backup from before hashes were kept: nothing tells PoE Redux's bytes from the game's. As those
            // versions did, a targeted file is patched as it is, and any other file gets its stored original.
            ours = item.Planned is null;
            source = ours ? store.ReadOriginal(path) : current;
        }
        else if (entry.Written.Count == 0 && entry.Original == currentHash)
        {
            source = current;
            ours = false;
        }
        else
        {
            // The game replaced the file after PoE Redux wrote it. The stored original is from the old game
            // version and is never written back; the file as it is now is the new original.
            item.Stale = true;
            if (item.Planned is null)
            {
                item.Outcome = Outcome.ReplacedByGame;
                return;
            }
            source = current;
            ours = false;
            entry = null;
        }

        var result = item.Planned?.Apply(source) ?? source;
        if (result.AsSpan().SequenceEqual(current))
        {
            item.Outcome = Outcome.Unchanged;
            return;
        }

        item.Content = result;
        if (ours && ReferenceEquals(result, source))
        {
            // No selected patch changes this file any more: its original goes back.
            item.Outcome = Outcome.Restore;
            item.RestoredHash = BackupStore.Hash(source);
            return;
        }

        item.Outcome = Outcome.Write;
        item.WrittenHash = BackupStore.Hash(result);
        if (entry is null)
            item.NewOriginal = current;
    }

    private static void ReadAndDecide(List<Work> work, BackupStore store, IProgress<PatchProgress>? progress)
    {
        var bundles = Enumerable.Range(0, work.Count).GroupBy(i => work[i].Record.BundleRecord).ToList();
        var openLock = new object();
        var done = 0;
        progress?.Report(new(PatchStage.Patching, 0, work.Count));

        void ProcessBundle(IGrouping<BundleRecord, int> group)
        {
            Bundle bundle;
            lock (openLock) // bundle factories, the GGPK one in particular, are not thread-safe
                bundle = OpenBundle(group.Key);

            using (bundle)
            {
                var items = group.OrderBy(i => work[i].Record.Offset).ToArray();
                for (var start = 0; start < items.Length;)
                {
                    var first = work[items[start]].Record;
                    var rangeStart = first.Offset;
                    var rangeEnd = first.Offset + first.Size;
                    var end = start + 1;
                    for (; end < items.Length; end++)
                    {
                        var next = work[items[end]].Record;
                        var nextEnd = Math.Max(rangeEnd, next.Offset + next.Size);
                        if (next.Offset / ChunkSize > Math.Max(rangeEnd - 1, 0) / ChunkSize + 1 || nextEnd - rangeStart > MaxRangeSize)
                            break;
                        rangeEnd = nextEnd;
                    }

                    var data = bundle.ReadWithoutCache(rangeStart, rangeEnd - rangeStart);
                    for (var k = start; k < end; k++)
                    {
                        var item = work[items[k]];
                        var current = data.AsSpan(item.Record.Offset - rangeStart, item.Record.Size).ToArray();
                        Decide(item, current, store);

                        var count = Interlocked.Increment(ref done);
                        if (count % ProgressStep == 0 || count == work.Count)
                            progress?.Report(new(PatchStage.Patching, count, work.Count));
                    }
                    start = end;
                }
            }
        }

        try
        {
            Parallel.ForEach(bundles, new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount }, ProcessBundle);
        }
        catch (AggregateException e)
        {
            // Report the first real error instead of "One or more errors occurred".
            ExceptionDispatchInfo.Throw(e.InnerExceptions[0]);
        }
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
