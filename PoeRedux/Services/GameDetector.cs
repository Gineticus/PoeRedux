using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace PoeRedux.Services;

public sealed record DetectedInstall(PoeGame Game, string Path, string Source);

/// <summary>
/// Locates PoE 1 / PoE 2 installs on this machine so the app can pre-fill the
/// GGPK / index path on startup. Sources scanned, in order:
///   1. Windows uninstall registry — catches the standalone GGG client
///      (and any other installer that registers an InstallLocation).
///   2. Steam — reads SteamPath, walks libraryfolders.vdf, probes each library's
///      steamapps/common for "Path of Exile" / "Path of Exile 2".
///
/// PoE 1 is shipping bundles-only now and may have no Content.ggpk, so the
/// install's containing folder name / DisplayName is the source of truth for
/// which game it is. Each probe is told which game to expect; it then returns
/// Content.ggpk if present, otherwise Bundles2\_.index.bin.
/// </summary>
public static class GameDetector
{
    private const string Poe1FileName = "Content.ggpk";
    private static readonly string Poe2RelativeIndex = Path.Combine("Bundles2", "_.index.bin");

    public static IReadOnlyList<DetectedInstall> Detect()
    {
        var hits = new List<DetectedInstall>();
        try { ScanUninstallRegistry(hits); } catch { }
        try { ScanSteam(hits); } catch { }

        return hits
            .GroupBy(h => Path.GetFullPath(h.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static void ScanUninstallRegistry(List<DetectedInstall> hits)
    {
        var probes = new (RegistryHive hive, RegistryView view)[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser,  RegistryView.Registry64),
        };

        foreach (var (hive, view) in probes)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;

            foreach (var name in uninstall.GetSubKeyNames())
            {
                RegistryKey? sub = null;
                try { sub = uninstall.OpenSubKey(name); }
                catch { continue; }
                if (sub == null) continue;

                using (sub)
                {
                    var display = sub.GetValue("DisplayName") as string;
                    var install = sub.GetValue("InstallLocation") as string;

                    if (string.IsNullOrWhiteSpace(install)) continue;
                    if (display == null ||
                        !display.Contains("Path of Exile", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // "Path of Exile 2" → PoE 2, anything else mentioning Path of Exile → PoE 1.
                    var game = display.Contains("Path of Exile 2", StringComparison.OrdinalIgnoreCase)
                        ? PoeGame.PoE2
                        : PoeGame.PoE1;

                    if (TryProbe(install.Trim('"', ' '), game, "GGG", out var hit))
                        hits.Add(hit);
                }
            }
        }
    }

    private static void ScanSteam(List<DetectedInstall> hits)
    {
        var steamPath = ReadSteamPath();
        if (steamPath == null) return;

        foreach (var lib in EnumerateSteamLibraries(steamPath))
        {
            var common = Path.Combine(lib, "steamapps", "common");
            if (!Directory.Exists(common)) continue;

            if (TryProbe(Path.Combine(common, "Path of Exile"),   PoeGame.PoE1, "Steam", out var poe1))
                hits.Add(poe1);
            if (TryProbe(Path.Combine(common, "Path of Exile 2"), PoeGame.PoE2, "Steam", out var poe2))
                hits.Add(poe2);
        }
    }

    private static string? ReadSteamPath()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
        {
            if (key?.GetValue("SteamPath") is string p && !string.IsNullOrWhiteSpace(p))
                return p.Replace('/', '\\');
        }

        using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (var key  = hklm.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
        {
            if (key?.GetValue("InstallPath") is string p && !string.IsNullOrWhiteSpace(p))
                return p;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSteamLibraries(string steamPath)
    {
        yield return steamPath;

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        string text;
        try { text = File.ReadAllText(vdf); }
        catch { yield break; }

        // Minimal VDF read: pull `"path" "<value>"` pairs. The file also encodes
        // app ids, sizes, etc. — we only need the library roots.
        foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var p = m.Groups[1].Value.Replace(@"\\", @"\");
            if (!string.Equals(p, steamPath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(p))
                yield return p;
        }
    }

    // The caller knows which game lives at `dir` (from folder name or DisplayName).
    // Prefer Content.ggpk when present, otherwise fall back to Bundles2\_.index.bin —
    // PoE 1 is migrating to bundle-only installs, so either may be the right input.
    private static bool TryProbe(string dir, PoeGame game, string source, out DetectedInstall hit)
    {
        hit = null!;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;

        var ggpk = Path.Combine(dir, Poe1FileName);
        if (File.Exists(ggpk))
        {
            hit = new DetectedInstall(game, ggpk, source);
            return true;
        }

        var index = Path.Combine(dir, Poe2RelativeIndex);
        if (File.Exists(index))
        {
            hit = new DetectedInstall(game, index, source);
            return true;
        }

        return false;
    }
}
