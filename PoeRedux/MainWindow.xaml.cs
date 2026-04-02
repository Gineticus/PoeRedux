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

namespace PoeRedux;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<PatchViewModel> _patches;
    private readonly ObservableCollection<ColorModsViewModel> _colorMods;
    private string _ggpkPath = string.Empty;
    private double _cameraZoom = 2.4;
    private string? _updateDownloadUrl;

    public MainWindow()
    {
        _patches = new ObservableCollection<PatchViewModel>();
        _colorMods = new ObservableCollection<ColorModsViewModel>();
        InitializeComponent();
        PatchesItemsControl.ItemsSource = _patches;
        
        UpdateStatus();

        SourceInitialized += (s, e) => ApplyDarkTitleBar();
        Loaded += async (s, e) => await CheckForUpdatesAsync();
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
            new ColorMods(),
            new Fog(),
            new EnvironmentParticles(),
            new Shadow(),
            new Light(),
            new Corpse(),
            new Delirium(),
            new Particles(),
            new Effects(),
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

        };

        foreach (var patch in patchInstances)
        {
            _patches.Add(new PatchViewModel(patch));
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

        if (ModsColorsButton != null)
        {
            ModsColorsButton.Visibility = _colorMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
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
            _ggpkPath = openFileDialog.FileName;
            GgpkPathTextBox.Text = _ggpkPath;
            UpdateStatus();
        }
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
        if (ZoomValueText != null)
        {
            ZoomValueText.Text = e.NewValue.ToString("F1").Replace(',', '.');
        }
        _cameraZoom = e.NewValue;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPatches = _patches.Where(p => p.IsSelected).ToList();

        foreach (var patch in selectedPatches)
        {
            if (patch.Patch is Camera cameraPatch)
            {
                cameraPatch.ZoomLevel = _cameraZoom;
            }
            if (patch.Patch is ColorMods colorModsPatch)
            {
                colorModsPatch.ColorModsOptions = _colorMods.Select(cm => cm.Option.Copy()).ToList();
            }
        }

        if (selectedPatches.Count == 0)
        {
            MessageBox.Show("Please select at least one patch to apply.", "No Patches Selected");
            return;
        }

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

        // Disable buttons during operation
        ApplyButton.IsEnabled = false;
        //ZoomSlider.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = patchesToApply.Count;
        ProgressBar.Value = 0;

        try
        {
            await Task.Run(() =>
            {
                if (_ggpkPath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    var index = new LibBundle3.Index(_ggpkPath, false);
                    index.ParsePaths();
                    PatchIndex(index, patchesToApply);
                    index.Dispose();
                }
                else if (_ggpkPath.EndsWith(".ggpk", StringComparison.OrdinalIgnoreCase))
                {
                    BundledGGPK ggpk = new(_ggpkPath, false);
                    var index = ggpk.Index;
                    index.ParsePaths();
                    PatchIndex(index, patchesToApply);
                    ggpk.Dispose();
                }
                else
                {
                    throw new InvalidDataException("The selected file is neither a GGPK nor an index BIN file.");
                }
            });

            StatusTextBlock.Text = $"Successfully applied {patchesToApply.Count} patch(es)!";
            MessageBox.Show($"Successfully applied {patchesToApply.Count} patch(es)!", "Success");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Error occurred while applying patches.";
            MessageBox.Show($"Error applying patches:\n\n{ex.Message}", "Error");
        }
        finally
        {
            // Re-enable buttons
            ApplyButton.IsEnabled = true;
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBar.Value = 0;
            //ZoomSlider.IsEnabled = true;
        }
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