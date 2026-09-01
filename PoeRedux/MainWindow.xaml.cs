using LibBundledGGPK3;
using Microsoft.Win32;
using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PoeRedux.Patches.Black;

namespace PoeRedux;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PatchViewModel> _patches;
    private readonly ObservableCollection<ColorModsViewModel> _colorMods;
    private readonly ObservableCollection<RollTierViewModel> _rollTiers = [];
    private readonly ObservableCollection<TierStatViewModel> _tierStats = [];
    private readonly ObservableCollection<TierGroupViewModel> _modGroups =
        [.. ModColorGroups.Defaults.Select(g => new TierGroupViewModel(g.Name, g.R, g.G, g.B, g.Tag))];
    private string _ggpkPath = string.Empty;
    private string? _updateDownloadUrl;
    private readonly ObservableCollection<MinimapTargetViewModel> _minimapIcons = [];
    private readonly StyleOptions _modStyle = new();
    private readonly StyleOptions _tierStyle = new();
    private readonly AppConfig _config = AppConfig.Load();
    private bool _restoring;
    private string? _lastGameKey;

    public MainWindow()
    {
        _patches = new ObservableCollection<PatchViewModel>();
        _colorMods = new ObservableCollection<ColorModsViewModel>();

        // InitializeComponent fires GameSelector's SelectionChanged, which builds the patch list,
        // so the flag has to be up before it runs or the restore lands on an empty list.
        _restoring = true;
        ApplySavedGroups();
        InitializeComponent();
        GameSelector.SelectedIndex = _config.SelectedGame;
        PatchesItemsControl.ItemsSource = GroupedPatches();
        GgpkPathTextBox.Text = SavedPathForCurrentGame();
        // Without this the first game switch has no previous key and drops that game's path.
        _lastGameKey = CurrentGameKey;
        _restoring = false;
        RestoreSelections();

        UpdateStatus();

        SourceInitialized += (s, e) => ApplyDarkTitleBar();
        Loaded += async (s, e) =>
        {
            UpdateRestoreButtonState();
            await CheckForUpdatesAsync();
            await CheckGameUpdateAsync();
        };
        Closing += (s, e) => SaveConfig();
    }

    /// <summary>
    /// The patch list under its category dividers. Sorting on the category's own order keeps the
    /// groups in the order they were designed in rather than alphabetical.
    /// </summary>
    private System.ComponentModel.ICollectionView GroupedPatches()
    {
        var view = new System.Windows.Data.CollectionViewSource { Source = _patches };
        view.GroupDescriptions.Add(
            new System.Windows.Data.PropertyGroupDescription(nameof(PatchViewModel.Category)));
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
            nameof(PatchViewModel.CategoryOrder), System.ComponentModel.ListSortDirection.Ascending));
        return view.View;
    }

    private Camera? CameraPatch => _patches.Select(p => p.Patch).OfType<Camera>().FirstOrDefault();

    private Minimap? MinimapPatch => _patches.Select(p => p.Patch).OfType<Minimap>().FirstOrDefault();

    private static string GameKey(int selectedIndex) => selectedIndex == 1 ? "PoE2" : "PoE1";

    private string CurrentGameKey => GameKey(GameSelector?.SelectedIndex ?? 0);

    private string SavedPathForCurrentGame() =>
        _config.GamePaths.TryGetValue(CurrentGameKey, out var path) ? path : string.Empty;

    /// <summary>Group colors are game independent, so they are applied before any patch list exists.</summary>
    private void ApplySavedGroups()
    {
        foreach (var group in _modGroups)
        {
            if (_config.ModGroups.TryGetValue(group.Name, out var hex))
                group.Hex = hex;
            if (_config.ModGroupTags.TryGetValue(group.Name, out var tag))
                group.Tag = tag;
        }
        _modStyle.TagPrefix = _config.ModTagPrefix;
        _tierStyle.TagPrefix = _config.TierTagPrefix;
        _tierStyle.PerLadder = _config.TierPerLadder;
        _tierStyle.MinLadderShare = _config.TierMinLadderShare;
        _tierStyle.CompactTier = _config.TierCompactTier;
    }

    /// <summary>Reapplies the saved patch checkboxes and mod list to whichever game is selected.</summary>
    private void RestoreSelections()
    {
        // A config written before the names lost their " Patch" suffix still has the old ones.
        var selected = _config.SelectedPatches.TryGetValue(CurrentGameKey, out var names)
            ? names.Select(PatchViewModel.DisplayName).ToHashSet(StringComparer.Ordinal)
            : [];
        if (names is not null)
            foreach (var patch in _patches)
                patch.IsSelected = selected.Contains(patch.Name);
        foreach (var patch in _patches.Where(p => FoldedKeys(p.Patch, selected).Any()))
            patch.IsSelected = true;
        RestoreEnvKeys(selected);
        RestoreSweepFolders();

        if (CameraPatch is Camera camera)
            camera.ZoomLevel = _config.CameraZoom;

        if (MinimapPatch is Minimap minimap)
        {
            minimap.RevealStrength = _config.MinimapReveal;
            minimap.WalkableHex = _config.MinimapWalkableHex;
            minimap.WalkableOpacity = _config.MinimapWalkableOpacity;
            minimap.EdgeHex = _config.MinimapEdgeHex;
            minimap.EdgeOpacity = _config.MinimapEdgeOpacity;
        }

        if (_config.ColorMods.TryGetValue(CurrentGameKey, out var mods) && mods.Count > 0)
        {
            _colorMods.Clear();
            foreach (var mod in mods)
                _colorMods.Add(new ColorModsViewModel(
                    new ColorModsOption(mod.Id, mod.Group, mod.Enabled, mod.Category)));
        }
    }

    private IEnumerable<IKeyTogglePatch> EnvPatches => _patches.Select(p => p.Patch).OfType<IKeyTogglePatch>();

    private IEnumerable<ISelectiveSweep> Sweeps => _patches.Select(p => p.Patch).OfType<ISelectiveSweep>();

    /// <summary>
    /// The protect list each sweep ships with, banked before a saved selection overwrites it, so
    /// Defaults in the picker can put it back.
    /// </summary>
    private readonly Dictionary<string, List<string>> _sweepDefaults = new(StringComparer.Ordinal);

    private string SweepKey(ISelectiveSweep sweep) =>
        $"{CurrentGameKey}:{PatchViewModel.DisplayName(((IPatch)sweep).Name)}";

    private void RestoreSweepFolders()
    {
        foreach (var sweep in Sweeps)
        {
            var key = SweepKey(sweep);
            if (!_sweepDefaults.ContainsKey(key))
                _sweepDefaults[key] = [.. sweep.DisabledPrefixes];

            if (!_config.SweepFoldersOff.TryGetValue(key, out var off))
                continue;
            sweep.DisabledPrefixes.Clear();
            foreach (var prefix in off)
                sweep.DisabledPrefixes.Add(prefix);
        }
    }

    private string EnvKeysKey(IKeyTogglePatch patch) =>
        $"{CurrentGameKey}:{PatchViewModel.DisplayName(patch.Name)}";

    private void RestoreEnvKeys(HashSet<string> selectedPatchNames)
    {
        foreach (var patch in EnvPatches)
        {
            patch.DisabledKeys.Clear();
            if (_config.EnvKeysOff.TryGetValue(EnvKeysKey(patch), out var off))
                foreach (var id in off)
                    patch.DisabledKeys.Add(id);
            else
                InheritFoldedPatches(patch, selectedPatchNames);
        }
    }

    /// <summary>
    /// Patches that became a single switch inside Environment Particles, and the switch they
    /// became. Each one corrupted or zeroed exactly what that switch does.
    /// </summary>
    private static readonly Dictionary<string, string> FoldedPatches = new(StringComparer.Ordinal)
    {
        ["Fog"] = "fog",
        ["Rain"] = "rain_intensity",
        ["Clouds"] = "clouds_intensity",
    };

    private static IEnumerable<string> FoldedKeys(IPatch patch, HashSet<string> selectedPatchNames) =>
        patch is not EnvKeyPatch env ? []
            : FoldedPatches
                .Where(f => selectedPatchNames.Contains(f.Key) && env.Keys.Any(k => k.Id == f.Value))
                .Select(f => f.Value);

    /// <summary>
    /// A config that selected one of the folded patches meant that switch and nothing else, so
    /// the rest of them start off and the install keeps behaving as it did.
    /// </summary>
    private static void InheritFoldedPatches(IKeyTogglePatch patch, HashSet<string> selectedPatchNames)
    {
        if (selectedPatchNames.Contains(PatchViewModel.DisplayName(patch.Name)))
            return;

        var inherited = FoldedKeys(patch, selectedPatchNames).ToHashSet(StringComparer.Ordinal);
        if (inherited.Count == 0)
            return;

        foreach (var key in patch.Keys.Where(k => !inherited.Contains(k.Id)))
            patch.DisabledKeys.Add(key.Id);
    }

    private void SaveConfig()
    {
        _config.SelectedGame = GameSelector?.SelectedIndex ?? 0;
        if (CameraPatch is Camera camera)
            _config.CameraZoom = camera.ZoomLevel;
        if (_ggpkPath.Length > 0)
            _config.GamePaths[CurrentGameKey] = _ggpkPath;

        _config.SelectedPatches[CurrentGameKey] =
            _patches.Where(p => p.IsSelected).Select(p => p.Name).ToList();
        _config.ColorMods[CurrentGameKey] = _colorMods
            .Select(cm => new AppConfig.ColorModEntry
            {
                Id = cm.Name,
                Group = cm.Group,
                Enabled = cm.IsSelected,
                Category = cm.Category,
            })
            .ToList();

        foreach (var patch in EnvPatches)
            _config.EnvKeysOff[EnvKeysKey(patch)] = [.. patch.DisabledKeys];

        foreach (var sweep in Sweeps)
            _config.SweepFoldersOff[SweepKey(sweep)] = [.. sweep.DisabledPrefixes];

        if (MinimapPatch is Minimap minimap)
        {
            _config.MinimapReveal = minimap.RevealStrength;
            _config.MinimapWalkableHex = minimap.WalkableHex;
            _config.MinimapWalkableOpacity = minimap.WalkableOpacity;
            _config.MinimapEdgeHex = minimap.EdgeHex;
            _config.MinimapEdgeOpacity = minimap.EdgeOpacity;
        }

        _config.ModGroups = _modGroups.ToDictionary(g => g.Name, g => g.Hex, StringComparer.Ordinal);
        _config.ModGroupTags = ModGroupTags();
        _config.ModTagPrefix = _modStyle.TagPrefix;
        _config.TierTagPrefix = _tierStyle.TagPrefix;
        _config.TierPerLadder = _tierStyle.PerLadder;
        _config.TierMinLadderShare = _tierStyle.MinLadderShare;
        _config.TierCompactTier = _tierStyle.CompactTier;
        if (_minimapIcons.Count > 0)
            _config.MinimapIcons = _minimapIcons
                .Select(t => new AppConfig.MinimapIconEntry
                {
                    Vanilla = t.VanillaIcon,
                    Icon = t.ChosenIcon,
                    Category = t.Category,
                    Enabled = t.IsSelected,
                    PathOverrides = t.PathChoices
                        .Where(pc => pc.HasOverride)
                        .ToDictionary(pc => pc.Path, pc => pc.OverrideIcon!, StringComparer.Ordinal),
                })
                .ToList();
        if (_rollTiers.Count > 0)
            _config.RollTiers = _rollTiers.Select(t => new AppConfig.RollTierEntry
            {
                Name = t.Name, Percent = t.MinPercent, Hex = t.Hex, Tag = t.Tag, Enabled = t.IsEnabled,
            }).ToList();
        // Only the stats that were unchecked, since everything is highlighted by default.
        if (_tierStats.Count > 0)
            _config.TierStatsDisabled = _tierStats.Where(s => !s.IsSelected).Select(s => s.StatId).ToList();

        _config.Save();
    }

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            IntPtr hwnd = hwndSource.Handle;

            // Use DWMWA_USE_IMMERSIVE_DARK_MODE (20) for Windows 11 / Windows 10 build 19041+
            int attribute = 20;
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(hwnd, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void InitializePoe1Patches()
    {
        var patchInstances = new IPatch[]
        {
            new Camera(),
            new Minimap(),
            new Patches.MinimapIcons(),
            new ColorMods(),
            new ModTierColors(),
            new Patches.ClientStrings(),
            new MonsterHP(),
            new EnvironmentParticles(),
            new Shadow(),
            new Light(),
            new Corpse(),
            new Delirium(),
            new Particles(),
            new Effects(),
            new Mtx(),
            new PostProcess(),
            new LoadingScreen(),
            new BlackScreen(),
            // new Aoc(),
            // new Env(),
            // new Epk(),
            // new Ffx(),
            // new Hlsl(),
            // new Mat(),
            // new NoCorpse(),
            // new Pet(),
        };

        foreach (var patch in patchInstances)
        {
            _patches.Add(new PatchViewModel(patch));
            if (patch is ColorMods colorModsPatch)
            {
                foreach (var option in colorModsPatch.ColorModsOptions)
                {
                    _colorMods.Add(new ColorModsViewModel(option.Copy()));
                }
            }
        }
    }

    private void InitializePoe2Patches()
    {
        var patchInstances = new IPatch[]
        {
            new Camera(),
            new Minimap(),
            new ColorMods2(),
            new MonsterHP(),
            new AtlasFog(),
            new EnvironmentParticles2(),
            new Shadow(),
            new Light(),
            new Delirium(),
            new Particles(),
            new Effects(),
            new Mtx(),
            new PostProcess(),
            new BlackScreen(),
            // new Aoc(),
            // new Env(),
            // new Epk(),
            // new Ffx(),
            // new Hlsl(),
            // new Mat(),
            // new NoCorpse(),
            // new Pet(),
        };

        foreach (var patch in patchInstances)
        {
            _patches.Add(new PatchViewModel(patch));
            if (patch is ColorMods2 colorModsPatch)            {
                foreach (var option in colorModsPatch.ColorModsOptions)
                {
                    _colorMods.Add(new ColorModsViewModel(option.Copy()));
                }
            }
        }
    }

    private void GameSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_patches == null || _colorMods == null) return;

        var selectedIndex = ((System.Windows.Controls.ComboBox)sender).SelectedIndex;

        _patches.Clear();
        _colorMods.Clear();

        if (selectedIndex == 0) // PoE 1
        {
            InitializePoe1Patches();
        }
        else if (selectedIndex == 1) // PoE 2
        {
            InitializePoe2Patches();
        }

        if (!_restoring)
        {
            // The outgoing game's path has to be banked before the key changes under us.
            if (_lastGameKey is not null && _ggpkPath.Length > 0)
                _config.GamePaths[_lastGameKey] = _ggpkPath;
            GgpkPathTextBox.Text = SavedPathForCurrentGame();
            RestoreSelections();
        }
        _lastGameKey = GameKey(selectedIndex);

        UpdateRestoreButtonState();
        _ = CheckGameUpdateAsync();
    }

    /// <summary>The gear on a patch row opens whichever editor that patch owns.</summary>
    private void PatchConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PatchViewModel patch)
            return;

        switch (patch.Patch)
        {
            case ColorMods or ColorMods2:
                ModsColorsButton_Click(sender, e);
                break;
            case ModTierColors:
                TierColorsButton_Click(sender, e);
                break;
            case Patches.MinimapIcons:
                MinimapIconsButton_Click(sender, e);
                break;
            case IKeyTogglePatch toggle:
                if (EnvKeysEditor.Show(toggle, this))
                    SaveConfig();
                break;
            case Camera camera:
                if (CameraZoomEditor.Show(camera, this))
                    SaveConfig();
                break;
            case Minimap minimap:
                if (MinimapRevealEditor.Show(minimap, this))
                    SaveConfig();
                break;
            case ISelectiveSweep sweep:
                OpenFolderPicker(sweep);
                break;
        }
    }

    /// <summary>
    /// The picker reads the folder tree out of the game files, so it needs a game selected before
    /// it can show anything.
    /// </summary>
    private void OpenFolderPicker(ISelectiveSweep clicked)
    {
        if (_ggpkPath.Length == 0 || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Select the game file first. The folder list is read from it.", "No Game File");
            return;
        }

        // Both sweeps are in the same dialog, on the row's own tab, since choosing one usually
        // means looking at the other.
        var tabs = Sweeps
            .Select(s => new EffectSweepViewModel(
                s,
                PatchViewModel.DisplayName(((IPatch)s).Name),
                _sweepDefaults.GetValueOrDefault(SweepKey(s)) ?? []))
            .ToList();

        var startTab = Math.Max(0, tabs.FindIndex(t => ReferenceEquals(t.Sweep, clicked)));
        if (EffectFoldersEditor.Show(tabs, _ggpkPath, startTab, this))
            SaveConfig();
    }

    private void ModsColorsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = ColorModsEditor.Show(_colorMods, _modGroups, _modStyle, _ggpkPath);
        if (result == true)
        {
            foreach (var colorMod in _colorMods)
            {
                colorMod.Option.Group = colorMod.Group;
                colorMod.Option.IsEnabled = colorMod.IsSelected;
            }
        }
        else
        {
            foreach (var colorMod in _colorMods)
            {
                colorMod.IsSelected = colorMod.Option.IsEnabled;
                colorMod.Group = colorMod.Option.Group;
            }
        }
        SaveConfig();
    }

    private void MinimapIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_minimapIcons.Count == 0)
            foreach (var saved in _config.MinimapIcons)
                _minimapIcons.Add(new MinimapTargetViewModel(
                    new MinimapIconTargets.Target(saved.Vanilla, saved.Category, PathsFor(saved.Vanilla)),
                    saved.Icon, saved.PathOverrides) { IsSelected = saved.Enabled });

        // The dialog edits the collection in place, so Exit keeps whatever was changed.
        MinimapIconsEditor.Show(_minimapIcons, _ggpkPath, CacheMinimapScan);
        SaveConfig();
    }

    /// <summary>
    /// Rebuilds a saved row's file list from the cached scan, because the ggpk may already hold
    /// the restyled value and no longer report the vanilla icon.
    /// </summary>
    private List<string> PathsFor(string vanillaIcon) =>
        _config.MinimapVanilla
            .Where(kv => kv.Value.Equals(vanillaIcon, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();

    private void CacheMinimapScan(List<MinimapIconTargets.Target> targets)
    {
        foreach (var target in targets)
            foreach (var path in target.Paths)
                // A path already in the cache keeps its vanilla value; the scan may be seeing
                // an icon this tool wrote earlier.
                _config.MinimapVanilla.TryAdd(path, target.Icon);

        // Saved rows scanned before the cache existed have no paths yet.
        foreach (var row in _minimapIcons.Where(r => r.Paths.Count == 0).ToList())
        {
            var paths = PathsFor(row.VanillaIcon);
            if (paths.Count == 0)
                continue;
            var index = _minimapIcons.IndexOf(row);
            var overrides = row.PathChoices
                .Where(pc => pc.HasOverride)
                .ToDictionary(pc => pc.Path, pc => pc.OverrideIcon!, StringComparer.Ordinal);
            _minimapIcons[index] = new MinimapTargetViewModel(
                new MinimapIconTargets.Target(row.VanillaIcon, row.Category, paths), row.ChosenIcon, overrides)
            {
                IsSelected = row.IsSelected,
            };
        }
    }

    private void ApplySavedTierStats(ObservableCollection<TierStatViewModel> stats)
    {
        var disabled = _config.TierStatsDisabled.ToHashSet(StringComparer.Ordinal);
        foreach (var stat in stats)
            if (disabled.Contains(stat.StatId))
                stat.IsSelected = false;
    }

    private Dictionary<string, (byte R, byte G, byte B)> ModGroupColors() =>
        _modGroups.ToDictionary(g => g.Name, g => (g.R, g.G, g.B), StringComparer.Ordinal);

    private Dictionary<string, string> ModGroupTags() =>
        _modGroups.ToDictionary(g => g.Name, g => g.Tag, StringComparer.Ordinal);

    private void TierColorsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rollTiers.Count == 0)
            foreach (var tier in RollTiers.Defaults)
            {
                var saved = _config.RollTiers.FirstOrDefault(t => t.Name == tier.Name);
                var model = new RollTierViewModel(tier);
                if (saved is not null)
                {
                    model.MinPercent = saved.Percent;
                    model.Hex = saved.Hex;
                    if (saved.Tag.Length > 0)
                        model.Tag = saved.Tag;
                    model.IsEnabled = saved.Enabled;
                }
                _rollTiers.Add(model);
            }

        // The dialog edits these collections in place, so Exit keeps whatever was changed.
        TierColorsEditor.Show(_rollTiers, _tierStats, _tierStyle, _ggpkPath, ApplySavedTierStats);
        SaveConfig();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "GGPK Files (*.ggpk;*.bin)|*.ggpk;*.bin|All Files (*.*)|*.*",
            Title = "Select GGPK or Index File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            GgpkPathTextBox.Text = openFileDialog.FileName;
            _ = CheckGameUpdateAsync();
        }
    }

    private void GgpkPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _ggpkPath = GgpkPathTextBox.Text.Trim();
        UpdateStatus();
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var patch in _patches)
        {
            patch.IsSelected = true;
        }
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var patch in _patches)
        {
            patch.IsSelected = false;
        }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPatches = _patches.Where(p => p.IsSelected).ToList();

        foreach (var patch in selectedPatches)
        {
            if (patch.Patch is ColorMods colorModsPatch)
            {
                colorModsPatch.ColorModsOptions = _colorMods.Select(cm => cm.Option.Copy()).ToList();
                colorModsPatch.GroupColors = ModGroupColors();
                colorModsPatch.GroupTags = ModGroupTags();
                colorModsPatch.TagPrefix = _modStyle.TagPrefix;
            }
            if (patch.Patch is ColorMods2 colorModsPatch2)
            {
                colorModsPatch2.ColorModsOptions = _colorMods.Select(cm => cm.Option.Copy()).ToList();
                colorModsPatch2.GroupColors = ModGroupColors();
                colorModsPatch2.GroupTags = ModGroupTags();
                colorModsPatch2.TagPrefix = _modStyle.TagPrefix;
            }
            if (patch.Patch is Patches.MinimapIcons minimapIconsPatch)
            {
                minimapIconsPatch.Overrides = _minimapIcons
                    .Where(t => t.IsSelected)
                    .SelectMany(t => t.PathChoices.Select(pc => (pc.Path, pc.EffectiveIcon, t.VanillaIcon)))
                    .Where(x => x.EffectiveIcon != x.VanillaIcon)
                    .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().EffectiveIcon, StringComparer.OrdinalIgnoreCase);
            }
            if (patch.Patch is ModTierColors tierColorsPatch)
            {
                // Both patches write the same files, so a mod the danger list owns keeps its color.
                tierColorsPatch.ExcludedStatIds = _colorMods
                    .Where(cm => cm.IsSelected)
                    .Select(cm => cm.Name)
                    .ToHashSet(StringComparer.Ordinal);
                tierColorsPatch.Tiers = _rollTiers.Select(t => t.ToTier()).ToList();
                // Never opened the editor means grade everything, as before it was configurable.
                tierColorsPatch.EnabledStatIds = _tierStats.Count == 0
                    ? null
                    : _tierStats.Where(s => s.IsSelected).Select(s => s.StatId).ToHashSet(StringComparer.Ordinal);
                tierColorsPatch.TagPrefix = _tierStyle.TagPrefix;
                tierColorsPatch.PerLadder = _tierStyle.PerLadder;
                tierColorsPatch.MinLadderShare = _tierStyle.MinLadderShare;
                tierColorsPatch.CompactTier = _tierStyle.CompactTier;
            }
        }

        if (selectedPatches.Count == 0)
        {
            MessageBox.Show("Please select at least one patch to apply.", "No Patches Selected");
            return;
        }

        SaveConfig();
        await ApplyPatches(selectedPatches);
    }

    private async Task ApplyPatches(List<PatchViewModel> patchesToApply)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Please select a valid GGPK file first.", "Invalid File");
            return;
        }

        StatusTextBlock.Text = "Starting patching process...";

        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;

        RunLog.Begin("Apply", game);
        RunLog.Line($"patches: {string.Join(", ", patchesToApply.Select(p => p.Name))}");

        // Disable buttons during operation
        ApplyButton.IsEnabled = false;
        if (RestoreButton != null) RestoreButton.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = patchesToApply.Count;
        ProgressBar.Value = 0;

        try
        {
            StatusTextBlock.Text = "Reaching the patch server for original file bytes...";
            using var vanillaSource = await VanillaSource.TryOpenCurrentAsync(game);
            RunLog.Line(vanillaSource is not null
                ? $"vanilla source: version {vanillaSource.Version}"
                : "vanilla source: unavailable, backups will fall back to the local install");
            StatusTextBlock.Text = "Starting patching process...";

            PatchOutcome outcome = new(new(0, 0), []);
            await Task.Run(() =>
            {
                BackupManager.SetVanillaSource(vanillaSource);
                if (_ggpkPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    using var index = new LibBundle3.Index(_ggpkPath, false);
                    index.ParsePaths();
                    outcome = RestoreThenPatch(index, game, patchesToApply);
                }
                else if (_ggpkPath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
                {
                    using BundledGGPK ggpk = new(_ggpkPath, false);
                    var index = ggpk.Index;
                    index.ParsePaths();
                    outcome = RestoreThenPatch(index, game, patchesToApply);
                }
                else
                {
                    throw new InvalidDataException("The selected file is neither a GGPK nor an index BIN file.");
                }
            });

            ReportOutcome(outcome, patchesToApply.Count, vanillaSource is not null);

            // Only a run that actually reached the server can confirm the game did not move
            // since; an unreachable-server run leaves whatever was recorded before untouched.
            if (vanillaSource is not null)
            {
                _config.LastAppliedVersion[CurrentGameKey] = vanillaSource.Version;
                SaveConfig();
                if (GameUpdateNotificationButton is not null)
                    GameUpdateNotificationButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            RunLog.Exception(ex);
            StatusTextBlock.Text = "Error occurred while applying patches.";
            MessageBox.Show($"Error applying patches:\n\n{ex.Message}", "Error");
        }
        finally
        {
            RunLog.End();
            // Re-enable buttons
            ApplyButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            UpdateRestoreButtonState();
        }
    }

    private async void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Please select a valid game file first.", "Invalid File");
            return;
        }

        if (!VanillaReset.IsSupported(_ggpkPath))
        {
            MessageBox.Show(
                "Reset works on Content.ggpk only. For a Steam or Epic install, verify the files through the launcher instead.",
                "Not Supported");
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            "This re-downloads every file that differs from the patch server.\n\n" +
            "All patches are reverted, including ones with no backup, and the client is updated " +
            "to the current version. Large download, and it must not be interrupted.\n\nContinue?",
            "Reset to Vanilla",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;

        ApplyButton.IsEnabled = false;
        ResetButton.IsEnabled = false;
        if (RestoreButton != null) RestoreButton.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        StatusTextBlock.Text = "Verifying against the patch server...";

        try
        {
            var changed = await Task.Run(() => VanillaReset.RunAsync(_ggpkPath, game));

            // Any backup now describes files that no longer exist in that state.
            BackupManager.DeleteBackup(game);

            StatusTextBlock.Text = $"Reset complete. {changed} file(s) updated.";
            MessageBox.Show(
                $"Reset complete. {changed} file(s) updated, added or removed.\n\nBackup cleared.",
                "Reset Complete");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Error occurred while resetting.";
            MessageBox.Show(
                $"Error resetting to vanilla:\n\n{ex.Message}\n\n" +
                "The ggpk may be partially updated. Run Reset again, or PackCheck.exe in the game folder.",
                "Error");
        }
        finally
        {
            ApplyButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Visibility = Visibility.Collapsed;
            UpdateRestoreButtonState();
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Please select a valid game file first.", "Invalid File");
            return;
        }

        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;

        if (!BackupManager.HasBackup(game))
        {
            MessageBox.Show("No backup found for this game.", "Nothing to Restore");
            return;
        }

        var fileCount = BackupManager.CountBackedUpFiles(game);
        StatusTextBlock.Text = $"Restoring {fileCount} file(s)...";

        RunLog.Begin("Restore", game);

        ApplyButton.IsEnabled = false;
        if (RestoreButton != null) RestoreButton.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = fileCount;
        ProgressBar.Value = 0;

        var result = new BackupManager.RestoreResult(0, 0);
        try
        {
            await Task.Run(() =>
            {
                void Progress(int done, int total)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        StatusTextBlock.Text = $"Restoring ({done}/{total})";
                        ProgressBar.Value = done;
                    });
                }

                if (_ggpkPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    using var index = new LibBundle3.Index(_ggpkPath, false);
                    index.ParsePaths();
                    result = BackupManager.Restore(index, game, Progress);
                    index.Save();
                }
                else if (_ggpkPath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
                {
                    using BundledGGPK ggpk = new(_ggpkPath, false);
                    var index = ggpk.Index;
                    index.ParsePaths();
                    result = BackupManager.Restore(index, game, Progress);
                    index.Save();
                }
                else
                {
                    throw new InvalidDataException("The selected file is neither a GGPK nor an index BIN file.");
                }
            });

            BackupManager.DeleteBackup(game);
            StatusTextBlock.Text = $"Restored {result.Restored} file(s) to original.";
            MessageBox.Show(
                $"Restored {result.Restored} file(s) to original.\n\nBackup cleared."
                + (result.Skipped > 0 ? $"\n\n{result.Skipped} file(s) were left as they are. {SkippedFilesNote}" : ""),
                "Restore Complete");
        }
        catch (Exception ex)
        {
            RunLog.Exception(ex);
            StatusTextBlock.Text = "Error occurred while restoring.";
            MessageBox.Show($"Error restoring:\n\n{ex.Message}", "Error");
        }
        finally
        {
            RunLog.End();
            ApplyButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            UpdateRestoreButtonState();
        }
    }

    private void ViewChangesButton_Click(object sender, RoutedEventArgs e)
    {
        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;

        if (!BackupManager.HasBackup(game))
        {
            MessageBox.Show("No backup found for this game.", "Nothing to Show");
            return;
        }
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Select a valid game file first.", "No Game File");
            return;
        }

        ChangesWindow.Show(game, _ggpkPath, this);
    }

    private void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;

        if (!BackupManager.HasBackup(game))
        {
            MessageBox.Show("No backup found for this game.", "Nothing to Delete");
            UpdateRestoreButtonState();
            return;
        }

        BackupManager.DeleteBackup(game);
        StatusTextBlock.Text = "Backup deleted.";
        UpdateRestoreButtonState();
    }

    /// <summary>
    /// Compares the server's current version against the one the last successful Apply ran
    /// against, and shows the reapply banner when they differ. Nothing to compare against, no
    /// prior backup, or an unreachable server all leave the banner exactly as it was: a check
    /// that cannot confirm an update happened must not claim one did, or hide a real one.
    /// </summary>
    private async Task CheckGameUpdateAsync()
    {
        if (GameUpdateNotificationButton is null) return;

        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;
        if (!BackupManager.HasBackup(game)
            || !_config.LastAppliedVersion.TryGetValue(CurrentGameKey, out var applied))
        {
            GameUpdateNotificationButton.Visibility = Visibility.Collapsed;
            return;
        }

        var current = await VanillaSource.TryGetCurrentVersionAsync(game);
        if (current is null) return;

        var updated = !string.Equals(current, applied, StringComparison.Ordinal);
        GameUpdateNotificationButton.Content = $"Game updated to {current}, click Apply to reapply";
        GameUpdateNotificationButton.Visibility = updated ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateRestoreButtonState()
    {
        if (RestoreButton == null) return;
        var game = GameSelector?.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;
        var count = BackupManager.CountBackedUpFiles(game);
        RestoreButton.IsEnabled = count > 0;
        RestoreButton.Content = count > 0 ? $"Restore Original ({count})" : "Restore Original";
        if (DeleteBackupButton != null) DeleteBackupButton.IsEnabled = count > 0;
        if (ViewChangesButton != null) ViewChangesButton.IsEnabled = count > 0;
    }

    /// <summary>
    /// Puts every previously patched file back before patching again, so a settings change lands
    /// on vanilla files. Patches read what is already there, and some refuse to touch their own
    /// output, so applying twice in a row would otherwise keep the first result.
    /// </summary>
    private PatchOutcome RestoreThenPatch(LibBundle3.Index index, PoeGame game, List<PatchViewModel> patches)
    {
        var revert = new BackupManager.RestoreResult(0, 0);
        if (BackupManager.HasBackup(game))
        {
            Dispatcher.Invoke(() => StatusTextBlock.Text = "Reverting previous patches...");
            revert = BackupManager.Restore(index, game);
            index.Save();
            // The originals are vanilla again, so the old backup is stale.
            BackupManager.DeleteBackup(game);
        }

        BackupManager.Begin(game);
        PatchReport.Begin();
        List<PatchReport.Miss> misses;
        try
        {
            PatchIndex(index, patches);
            BackupManager.Seal(index);
        }
        finally
        {
            BackupManager.End();
            misses = PatchReport.End();
        }
        return new PatchOutcome(revert, misses);
    }

    private sealed record PatchOutcome(BackupManager.RestoreResult Revert, List<PatchReport.Miss> Misses);

    private const string SkippedFilesNote =
        "Files something else has written since the last apply, a game update above all, were " +
        "left as they are: their stored original belongs to the previous version. Use Reset to " +
        "Vanilla if the install needs to go back to a clean state.";

    private void ReportOutcome(PatchOutcome outcome, int applied, bool cdnAvailable)
    {
        RunLog.Line($"outcome: reverted {outcome.Revert.Restored}, revert-skipped {outcome.Revert.Skipped}, "
            + $"applied {applied} patch(es), misses {outcome.Misses.Count}, "
            + $"local-fallback backups {BackupManager.VanillaFallbackCount}");

        var summary = outcome.Revert.Restored > 0
            ? $"Reverted {outcome.Revert.Restored} file(s), then applied {applied} patch(es)."
            : $"Applied {applied} patch(es).";

        var skipped = outcome.Revert.Skipped > 0
            ? $"\n\n{outcome.Revert.Skipped} file(s) were not reverted. {SkippedFilesNote}"
            : "";

        // Backups are meant to hold the patch server's own bytes, not whatever the local install
        // happened to have. Say so when that could not be guaranteed, or Restore Original's
        // honesty is a promise nobody can see was kept.
        var fallback = BackupManager.VanillaFallbackCount;
        var vanillaNote = !cdnAvailable
            ? "\n\nCould not reach the patch server, so new backup entries were taken from the " +
              "local install instead of confirmed vanilla bytes."
            : fallback > 0
                ? $"\n\n{fallback} new backup entr{(fallback == 1 ? "y was" : "ies were")} taken " +
                  "from the local install: the patch server has no record of that path."
                : "";

        if (outcome.Misses.Count == 0)
        {
            StatusTextBlock.Text = summary;
            MessageBox.Show(summary + skipped + vanillaNote, "Success");
            return;
        }

        var affected = outcome.Misses.Select(m => m.Patch).Distinct(StringComparer.Ordinal).ToList();
        var detail = string.Join('\n', outcome.Misses.Select(m => $"  {m.Patch}: {m.Detail}"));

        StatusTextBlock.Text = $"{summary} {affected.Count} patch(es) found nothing to change.";
        MessageBox.Show(
            $"{summary}{skipped}{vanillaNote}\n\n{affected.Count} patch(es) found nothing to change, most likely " +
            "because a game update moved what they look for:\n\n" + detail,
            "Applied With Warnings");
    }

    private void PatchIndex(LibBundle3.Index index, List<PatchViewModel> patches)
    {
        var fileTree = index.BuildTree(true);

        for (int i = 0; i < patches.Count; i++)
        {
            var patch = patches[i];

            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = $"Applying {patch.Name} ({i + 1}/{patches.Count})...";
                ProgressBar.Value = i;
            });

            PatchReport.Entering(patch.Name);
            patch.Patch.Apply(fileTree);
            index.Save();

            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = i + 1;
            });
        }
    }

    private void UpdateStatus()
    {
        if (string.IsNullOrEmpty(_ggpkPath))
        {
            StatusTextBlock.Text = "Please select a GGPK file to begin.";
        }
        else
        {
            StatusTextBlock.Text = $"Ready - {Path.GetFileName(_ggpkPath)}";
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        const string githubOwner = "Gineticus";
        const string githubRepo = "PoeRedux";

        try
        {
            var updateInfo = await GitHubUpdateChecker.CheckForUpdatesAsync(githubOwner, githubRepo);

            if (updateInfo?.IsUpdateAvailable == true)
            {
                _updateDownloadUrl = updateInfo.DownloadUrl;
                UpdateNotificationButton.Content = $"Update available: {updateInfo.LatestVersion}";
                UpdateNotificationButton.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Silently ignore update check failures
        }
    }

    private void UpdateNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_updateDownloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateDownloadUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show("Could not open the download page. Please visit the GitHub releases page manually.", "Error");
            }
        }
    }
}