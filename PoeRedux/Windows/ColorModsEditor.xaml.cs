using PoeRedux.Models;
using PoeRedux.Services;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Threading;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Interop;

namespace PoeRedux;

public partial class ColorModsEditor : Window
{
    private readonly ObservableCollection<ColorModsViewModel> _colorMods;
    private readonly string _gameFilePath;
    private readonly ObservableCollection<StatCatalogEntry> _catalog = new();
    private CollectionViewSource? _catalogView;
    private bool _catalogRequested;
    private readonly DispatcherTimer _searchDebounce;
    private string _query = "";
    private int _shown;

    public ColorModsEditor(
        ObservableCollection<ColorModsViewModel> colorMods,
        ObservableCollection<TierGroupViewModel> groups,
        StyleOptions style,
        string gameFilePath = "")
    {
        InitializeComponent();
        _colorMods = colorMods;
        _gameFilePath = gameFilePath;
        GroupsItemsControl.ItemsSource = groups;
        TagPrefixCheckBox.IsChecked = style.TagPrefix;
        TagPrefixCheckBox.Checked += (s, e) => style.TagPrefix = true;
        TagPrefixCheckBox.Unchecked += (s, e) => style.TagPrefix = false;

        var grouped = new CollectionViewSource { Source = colorMods };
        grouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ColorModsViewModel.Category)));
        ColorModsItemsControl.ItemsSource = grouped.View;

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _searchDebounce.Tick += (s, e) => ApplySearch();

        Loaded += async (s, e) => await LoadCatalogAsync();
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private async Task LoadCatalogAsync()
    {
        if (_catalogRequested) return;
        _catalogRequested = true;

        if (string.IsNullOrEmpty(_gameFilePath) || !File.Exists(_gameFilePath))
        {
            CatalogStatusText.Text = "Select a valid game file to list all mods";
            return;
        }

        CatalogStatusText.Text = "Reading stat descriptions...";
        try
        {
            var entries = await StatCatalog.LoadAsync(_gameFilePath);
            var listed = _colorMods.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
            // Offer both the curated categories and every one the game data implies.
            var categories = _colorMods.Select(m => m.Category)
                .Concat(entries.Select(e => e.Category))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            foreach (var entry in entries)
            {
                entry.IsAdded = listed.Contains(entry.Id);
                entry.AvailableCategories = categories;
                _catalog.Add(entry);
            }

            _catalogView = new CollectionViewSource { Source = _catalog };
            _catalogView.Filter += CatalogFilter;
            CatalogListBox.ItemsSource = _catalogView.View;
            CatalogStatusText.Text = $"{_catalog.Count} mods";
        }
        catch (Exception ex)
        {
            CatalogStatusText.Text = "Failed to read mods";
            MessageBox.Show($"Could not read the stat descriptions:\n\n{ex.Message}", "Error");
        }
    }

    private void CatalogFilter(object sender, FilterEventArgs e)
    {
        if (e.Item is not StatCatalogEntry entry)
        {
            e.Accepted = false;
            return;
        }
        e.Accepted = _query.Length == 0
            || entry.Id.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || entry.Text.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(_query, StringComparison.OrdinalIgnoreCase);
        // Counting here avoids a second walk over 15k rows just to show a total.
        if (e.Accepted)
            _shown++;
    }

    private void CatalogSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CatalogSearchHint.Visibility = CatalogSearchBox.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        CatalogClearButton.Visibility = CatalogSearchHint.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Refiltering on every keystroke stutters at this list size, so wait for a pause.
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void ApplySearch()
    {
        _searchDebounce.Stop();
        if (_catalogView is null)
            return;

        _query = CatalogSearchBox.Text.Trim();
        _shown = 0;
        _catalogView.View.Refresh();
        CatalogStatusText.Text = _query.Length == 0
            ? $"{_catalog.Count} mods"
            : $"{_shown} of {_catalog.Count} mods";
    }

    private void CatalogClearButton_Click(object sender, RoutedEventArgs e)
    {
        CatalogSearchBox.Clear();
        ApplySearch();
        CatalogSearchBox.Focus();
    }

    private void ToggleModButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not StatCatalogEntry entry)
            return;

        var existing = _colorMods.FirstOrDefault(m => m.Name == entry.Id);
        if (existing is null)
        {
            _colorMods.Add(new ColorModsViewModel(
                new ColorModsOption(entry.Id, ModColorGroups.Fallback, true, entry.SelectedCategory)));
            entry.IsAdded = true;
        }
        else
        {
            RemoveFromList(existing);
            entry.IsAdded = false;
        }
    }

    private void RemoveModButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ColorModsViewModel mod)
            return;
        RemoveFromList(mod);
    }

    private void RemoveFromList(ColorModsViewModel mod)
    {
        // Disable on the way out so the next apply strips an annotation written earlier.
        mod.IsSelected = false;
        mod.Option.IsEnabled = false;
        _colorMods.Remove(mod);

        var entry = _catalog.FirstOrDefault(c => c.Id == mod.Name);
        if (entry is not null)
            entry.IsAdded = false;
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

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var colorMods = _colorMods;
        if (colorMods == null)
        {
            MessageBox.Show("No color mods to save.", "Error");
            return;
        }
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "color_mods.json"
        };
        if (saveFileDialog.ShowDialog() == true)
        {
            var colorModsDict = new Dictionary<string, object>();
            foreach (var mod in colorMods)
            {
                colorModsDict[mod.Name] = new
                {
                    enabled = mod.IsSelected,
                    // Kept as "color" so a config written before groups existed still loads.
                    color = mod.Group,
                };
            }
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = System.Text.Json.JsonSerializer.Serialize(colorModsDict, options);
            System.IO.File.WriteAllText(saveFileDialog.FileName, json);
            MessageBox.Show("Color mods saved successfully.", "Success");
        }
    }

    private void LoadConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var colorMods = _colorMods;
        if (colorMods == null)
        {
            MessageBox.Show("No color mods to load.", "Error");
            return;
        }
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
        };
        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                var colorModsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>>(json);
                if (colorModsDict != null)
                {
                    foreach (var mod in colorMods)
                    {
                        if (colorModsDict.TryGetValue(mod.Name, out var modData))
                        {
                            if (modData.TryGetValue("enabled", out var enabledObj) 
                                && (enabledObj.ValueKind == System.Text.Json.JsonValueKind.True || enabledObj.ValueKind == System.Text.Json.JsonValueKind.False))
                            {
                                mod.IsSelected = enabledObj.GetBoolean();
                            }
                            if (modData.TryGetValue("color", out var colorObj) 
                                && colorObj.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                mod.Group = ModColorGroups.Normalize(colorObj.GetString() ?? "");
                            }
                        }
                    }
                    MessageBox.Show("Color mods loaded successfully.", "Success");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load color mods: {ex.Message}", "Error");
            }
        }
    }

    public static bool Show(
        ObservableCollection<ColorModsViewModel> colorMods,
        ObservableCollection<TierGroupViewModel> groups,
        StyleOptions style,
        string gameFilePath = "")
    {
        var dialog = new ColorModsEditor(colorMods, groups, style, gameFilePath);

        if (Application.Current.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        return dialog.ShowDialog() == true;
    }
}
