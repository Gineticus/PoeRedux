using LibBundledGGPK3;
using System.Runtime.CompilerServices;

namespace PoeRedux.Services;

public static class GameIndex
{
    /// <summary>Whether <paramref name="path"/> names a file PoE Redux can open: Content.ggpk or _.index.bin.</summary>
    public static bool IsSupported(string path) =>
        path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens the game index of a Steam or Epic install (_.index.bin) or a standalone one (Content.ggpk),
    /// runs <paramref name="action"/> on it, and gives the memory it used back to Windows.
    /// </summary>
    /// <param name="parsePaths">
    /// Whether to read the file paths. Apply needs them to build the file tree. Restore finds files by
    /// path hash and skips this, which saves 2 s on PoE 1 and 3.5 s on PoE 2.
    /// </param>
    public static T Use<T>(string path, bool parsePaths, Func<LibBundle3.Index, T> action)
    {
        try
        {
            return OpenAndRun(path, parsePaths, action);
        }
        finally
        {
            // The index has millions of records. Once it is closed, the .NET GC keeps the freed memory
            // (about 3 GB for PoE 2) for later use; an aggressive collection returns it so the game can use it.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }
    }

    // Kept out of line, so that nothing in Use still points to the index during the collection.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T OpenAndRun<T>(string path, bool parsePaths, Func<LibBundle3.Index, T> action)
    {
        if (path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            using var index = new LibBundle3.Index(path, false);
            if (parsePaths)
                index.ParsePaths();
            return action(index);
        }

        if (path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
        {
            using var ggpk = new BundledGGPK(path, false);
            if (parsePaths)
                ggpk.Index.ParsePaths();
            return action(ggpk.Index);
        }

        throw new InvalidDataException("The selected file is neither Content.ggpk nor _.index.bin.");
    }

    /// <summary>
    /// Folders under <paramref name="root"/> in the game, read without changing anything. Used to suggest
    /// folders to keep. The file is opened for reading and shared, so the game may be running.
    /// </summary>
    public static List<string> ListFolders(string path, string root)
    {
        try
        {
            return ReadFolders(path, root);
        }
        finally
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<string> ReadFolders(string path, string root)
    {
        var prefix = root.TrimEnd('/') + "/";
        var folders = new SortedSet<string>(StringComparer.Ordinal);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
        {
            using var ggpk = new BundledGGPK(stream, false, false);
            Collect(ggpk.Index);
        }
        else
        {
            using var index = new LibBundle3.Index(stream, false, false, new LibBundle3.DriveBundleFactory(Path.GetDirectoryName(Path.GetFullPath(path))!));
            Collect(index);
        }
        return [.. folders];

        void Collect(LibBundle3.Index index)
        {
            index.ParsePaths();
            foreach (var file in index.Files.Values)
            {
                if (file.Path is not { } p || !p.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                for (var slash = p.IndexOf('/', prefix.Length); slash > 0; slash = p.IndexOf('/', slash + 1))
                    folders.Add(p[..slash]);
            }
        }
    }
}
