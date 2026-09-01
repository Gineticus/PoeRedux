using LibGGPK3;

namespace PoeRedux.Services;

/// <summary>
/// Re-downloads every ggpk file whose hash does not match the patch server. Reverts edits
/// from any tool, including ones this app has no backup for, and lands the install on the
/// version the server currently serves.
/// </summary>
public static class VanillaReset
{
    public static bool IsSupported(string gameFilePath) =>
        gameFilePath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string ggpkPath, PoeGame game, CancellationToken cancellationToken = default)
    {
        using var ggpk = new GGPK(ggpkPath);
        using var patch = new PatchClient();
        await patch.ConnectAsync(game == PoeGame.PoE2
            ? PatchClient.ServerEndPoints.US2
            : PatchClient.ServerEndPoints.US);

        // Erasing the root hash forces a full walk instead of an early exit on a matching root.
        ggpk.EraseRootHash();
        var changed = await patch.UpdateNodeAsync(ggpk.Root, TimeSpan.FromMinutes(10), cancellationToken);
        ggpk.Flush();
        return changed;
    }
}
