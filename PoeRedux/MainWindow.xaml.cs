using LibBundledGGPK3;
using Microsoft.Win32;
using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Patches.Black;
using PoeRedux.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PoeRedux;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PatchViewModel> _patches = new();
    private readonly ObservableCollection<ColorModsViewModel> _colorMods = new();
    private string _ggpkPath = string.Empty;
    private string? _updateDownloadUrl;

    public MainWindow()
    {
        InitializeComponent();
        PatchesItemsControl.ItemsSource = _patches;

        UpdateStatus();

        DarkTitleBar.Enable(this);
        Loaded += async (s, e) =>
        {
            UpdateRestoreButtonState();
            await CheckForUpdatesAsync();
        };
    }

    private PoeGame SelectedGame => GameSelector.SelectedIndex == 1 ? PoeGame.PoE2 : PoeGame.PoE1;

    private static IPatch[] CreatePatches(PoeGame game)
    {
        if (game == PoeGame.PoE1)
        {
            return
            [
                new Camera(),
                new Minimap(),
                new ColorMods(PoeGame.PoE1),
                new MonsterHP(),
                new Fog(),
                new EnvironmentParticles(),
                new Shadow(),
                new Light(),
                new Corpse(),
                new Delirium(),
                new Particles(),
                new Effects(),
                new BlackScreen(),
                // new Aoc(),
                // new Env(),
                // new Epk(),
                // new Ffx(),
                // new Hlsl(),
                // new Mat(),
                // new NoCorpse(),
                // new Pet(),
            ];
        }

        return
        [
            new Camera(),
            new Minimap(),
            new ColorMods(PoeGame.PoE2),
            new MonsterHP(),
            new AtlasFog(),
            new Fog(),
            new Rain(),
            new Clouds(),
            new EnvironmentParticles2(),
            new Shadow(),
            new Light(),
            new Delirium(),
            new Particles(),
            new Effects(),
            new BlackScreen(),
            // new Aoc(),
            // new Env(),
            // new Epk(),
            // new Ffx(),
            // new Hlsl(),
            // new Mat(),
            // new NoCorpse(),
            // new Pet(),
        ];
    }

    private void GameSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _patches.Clear();
        _colorMods.Clear();

        foreach (var patch in CreatePatches(SelectedGame))
        {
            _patches.Add(new PatchViewModel(patch));
            if (patch is ColorMods colorMods)
            {
                foreach (var option in colorMods.ColorModsOptions)
                    _colorMods.Add(new ColorModsViewModel(option.Copy()));
            }
        }

        // The first call comes from InitializeComponent, before the controls below the selector exist.
        if (ModsColorsButton != null)
            ModsColorsButton.Visibility = _colorMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateRestoreButtonState();
    }

    private void ModsColorsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = ColorModsEditor.Show(_colorMods);
        if (result == true)
        {
            foreach (var colorMod in _colorMods)
            {
                colorMod.Option.Color = colorMod.SelectedColor;
                colorMod.Option.IsEnabled = colorMod.IsSelected;
            }
        }
        else
        {
            foreach (var colorMod in _colorMods)
            {
                colorMod.IsSelected = colorMod.Option.IsEnabled;
                colorMod.SelectedColor = colorMod.Option.Color;
            }
        }
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
        }
    }

    private void GgpkPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
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

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // The first call comes from InitializeComponent, before ZoomValueText exists.
        if (ZoomValueText != null)
            ZoomValueText.Text = e.NewValue.ToString("F1", CultureInfo.InvariantCulture);
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var patches = _patches.Where(p => p.IsSelected).Select(p => p.Patch).ToList();
        if (patches.Count == 0)
        {
            MessageBox.Show("Please select at least one patch to apply.", "No Patches Selected");
            return;
        }

        foreach (var patch in patches)
        {
            if (patch is Camera camera)
                camera.ZoomLevel = ZoomSlider.Value;
            else if (patch is ColorMods colorMods)
                colorMods.ColorModsOptions = _colorMods.Select(cm => cm.Option.Copy()).ToList();
        }

        await ApplyPatches(patches);
    }

    private async Task ApplyPatches(List<IPatch> patches)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Please select a valid GGPK file first.", "Invalid File");
            return;
        }

        StatusTextBlock.Text = "Starting patching process...";

        var path = _ggpkPath;
        var game = SelectedGame;
        var progress = new Progress<PatchProgress>(ShowPatchProgress);
        SetBusy(true);
        ProgressBar.IsIndeterminate = true;

        try
        {
            await Task.Run(() => UseIndex(path, index => PatchRunner.Run(index, patches, game, progress)));

            StatusTextBlock.Text = $"Successfully applied {patches.Count} patch(es)!";
            MessageBox.Show($"Successfully applied {patches.Count} patch(es)!", "Success");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Error occurred while applying patches.";
            MessageBox.Show($"Error applying patches:\n\n{ex.Message}", "Error");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_ggpkPath) || !File.Exists(_ggpkPath))
        {
            MessageBox.Show("Please select a valid game file first.", "Invalid File");
            return;
        }

        var game = SelectedGame;

        if (!BackupManager.HasBackup(game))
        {
            MessageBox.Show("No backup found for this game.", "Nothing to Restore");
            return;
        }

        var fileCount = BackupManager.CountBackedUpFiles(game);
        StatusTextBlock.Text = $"Restoring {fileCount} file(s)...";

        var path = _ggpkPath;
        var progress = new Progress<(int Done, int Total)>(p =>
        {
            StatusTextBlock.Text = $"Restoring ({p.Done}/{p.Total})";
            ProgressBar.Value = p.Done;
        });
        SetBusy(true);
        ProgressBar.Maximum = fileCount;

        try
        {
            var restored = await Task.Run(() => UseIndex(path, index => BackupManager.Restore(index, game, progress)));

            BackupManager.DeleteBackup(game);
            StatusTextBlock.Text = $"Restored {restored} file(s) to original.";
            MessageBox.Show($"Restored {restored} file(s) to original.\n\nBackup cleared.", "Restore Complete");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Error occurred while restoring.";
            MessageBox.Show($"Error restoring:\n\n{ex.Message}", "Error");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
    {
        var game = SelectedGame;

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
    /// Opens the game index of a Steam install (_.index.bin) or a standalone one (Content.ggpk)
    /// and runs <paramref name="action"/> on it.
    /// </summary>
    private static T UseIndex<T>(string path, Func<LibBundle3.Index, T> action)
    {
        if (path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            using var index = new LibBundle3.Index(path, false);
            index.ParsePaths();
            return action(index);
        }

        if (path.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
        {
            using var ggpk = new BundledGGPK(path, false);
            ggpk.Index.ParsePaths();
            return action(ggpk.Index);
        }

        throw new InvalidDataException("The selected file is neither a GGPK nor an index BIN file.");
    }

    // While game files change, the progress bar shows and the buttons that touch game files or backups are locked.
    // Deleting backups during Apply would lose originals, and switching games would turn Restore back on.
    private void SetBusy(bool busy)
    {
        ApplyButton.IsEnabled = !busy;
        GameSelector.IsEnabled = !busy;
        ProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Minimum = 0;
        ProgressBar.Value = 0;

        if (busy)
        {
            RestoreButton.IsEnabled = false;
            DeleteBackupButton.IsEnabled = false;
        }
        else
        {
            UpdateRestoreButtonState();
        }
    }

    private void UpdateRestoreButtonState()
    {
        // The first call comes from InitializeComponent, before the buttons exist.
        if (RestoreButton == null)
            return;

        var count = BackupManager.CountBackedUpFiles(SelectedGame);
        RestoreButton.IsEnabled = count > 0;
        RestoreButton.Content = count > 0 ? $"Restore Original ({count})" : "Restore Original";
        DeleteBackupButton.IsEnabled = count > 0;
    }

    private void ShowPatchProgress(PatchProgress progress)
    {
        ProgressBar.IsIndeterminate = progress.Total == 0;
        ProgressBar.Maximum = Math.Max(progress.Total, 1);
        ProgressBar.Value = progress.Done;
        StatusTextBlock.Text = progress.Stage switch
        {
            PatchStage.Patching => $"Patching files ({progress.Done}/{progress.Total})...",
            PatchStage.Writing => $"Updating game files ({progress.Done}/{progress.Total})...",
            PatchStage.Saving => "Saving changes...",
            _ => "Finding files to patch...",
        };
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
