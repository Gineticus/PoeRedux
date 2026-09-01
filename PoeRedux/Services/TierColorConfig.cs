using System.IO;

using LibBundledGGPK3;

using PoeRedux.Models;

namespace PoeRedux.Services;

/// <summary>Which stats have a craftable ceiling, so a roll can be judged against it.</summary>
public static class TierColorConfig
{

    /// <summary>
    /// Every stat with a craftable ceiling, with the sample text shown in game.
    /// </summary>
    public static Task<List<TierStatViewModel>> LoadStatsAsync(string gameFilePath) =>
        Task.Run(() =>
        {
            return GameFile.Read(gameFilePath, index =>
            {
                var scales = ModTierBands.Build(
                    ReadFile(index, "data/mods.datc64"),
                    ReadFile(index, "data/stats.datc64"),
                    out _);
                var texts = StatCatalog.Scan(index);

                return scales
                    .Select(kv => new TierStatViewModel(
                        kv.Key,
                        texts.TryGetValue(kv.Key, out var t) ? t : "",
                        kv.Value.Max,
                        kv.Value.ModCount))
                    .OrderBy(s => s.StatId, StringComparer.Ordinal)
                    .ToList();
            });
        });

    private static byte[] ReadFile(LibBundle3.Index index, string path) =>
        index.TryGetFile(path, out var file)
            ? file.Read().ToArray()
            : throw new FileNotFoundException("not in the game files: " + path);
}
