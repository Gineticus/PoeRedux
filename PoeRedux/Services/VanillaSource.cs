using System.IO;

using LibBundledGGPK3;
using LibGGPK3;

using BundleIndex = LibBundle3.Index;

namespace PoeRedux.Services;

/// <summary>
/// Vanilla file content straight from the patch server, cached locally per version so a repeat
/// run does not re-download what it already has. Used to seed a backup with the true original of
/// a file instead of whatever the local install currently holds, which may already carry another
/// tool's edits.
/// </summary>
public sealed class VanillaSource : IDisposable
{
    private readonly ServerBundleFactory _factory;
    private readonly BundleIndex _index;

    public string Version { get; }

    public VanillaSource(string cacheRoot, string version)
    {
        Version = version;
        var dir = Path.Combine(cacheRoot, version, "Bundles2") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(dir);
        _factory = new ServerBundleFactory(dir, $"https://patch.poecdn.com/{version}/");
        var indexPath = Path.Combine(dir, "_.index.bin");
        if (!File.Exists(indexPath))
            _factory.DownloadIndex();
        _index = new BundleIndex(indexPath, parsePaths: true, bundleFactory: _factory);
    }

    public bool TryRead(string path, out byte[] bytes)
    {
        if (_index.TryGetFile(path, out var file))
        {
            bytes = file.Read().ToArray();
            return true;
        }
        bytes = [];
        return false;
    }

    /// <summary>
    /// The version the patch server currently serves, or null when it cannot be reached. Cheap:
    /// no index or bundle download, just the redirect the server answers with. Meant for checking
    /// whether the game updated without paying for a full <see cref="TryOpenCurrentAsync"/>.
    /// </summary>
    public static async Task<string?> TryGetCurrentVersionAsync(PoeGame game)
    {
        try
        {
            var endpoint = game == PoeGame.PoE2 ? PatchClient.ServerEndPoints.US2 : PatchClient.ServerEndPoints.US;
            var url = await PatchClient.GetPatchCdnUrlAsync(endpoint);
            return url.TrimEnd('/').Split('/')[^1];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Opens the vanilla index for whatever version the patch server currently serves, or null
    /// when the server cannot be reached. A null result is not fatal to the caller: a backup can
    /// still be seeded from the local install, the situation this type exists to improve on but
    /// not the only thing it can fall back to.
    /// </summary>
    public static async Task<VanillaSource?> TryOpenCurrentAsync(PoeGame game)
    {
        var version = await TryGetCurrentVersionAsync(game);
        if (version is null) return null;

        try
        {
            var cacheRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PoeRedux", "CdnCache", game == PoeGame.PoE2 ? "poe2" : "poe1");
            return new VanillaSource(cacheRoot, version);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _index.Dispose();
        _factory.Dispose();
    }
}
