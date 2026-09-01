using LibBundledGGPK3;
using System.IO;
using BundleIndex = LibBundle3.Index;

namespace PoeRedux.Services;

/// <summary>
/// Opens the game archive for reading only, sharing the handle. LibGGPK3's path constructors
/// open read/write and exclusive, so anything else holding the file, the game itself or another
/// modding tool, makes every editor that only wants to read fail with a sharing violation.
/// </summary>
public static class GameFile
{
    public static bool IsGgpk(string path) =>
        path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase);

    private static FileStream OpenShared(string path) => File.Open(path, new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.ReadWrite,
    });

    /// <summary>
    /// Reads from either container form and hands the index to <paramref name="read"/>. The
    /// archive is closed before returning, so nothing is left holding the file.
    /// </summary>
    public static T Read<T>(string path, Func<BundleIndex, T> read)
    {
        if (IsGgpk(path))
        {
            using var ggpk = new BundledGGPK(OpenShared(path), leaveOpen: false, parsePathsInIndex: true);
            return read(ggpk.Index);
        }
        using var index = new BundleIndex(OpenShared(path), leaveOpen: false, parsePaths: true);
        return read(index);
    }
}
