using PoeRedux.Models;
using PoeRedux.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PoeRedux;

public partial class MinimapIconsEditor : Window
{
    private readonly ObservableCollection<MinimapTargetViewModel> _selected;
    private readonly ObservableCollection<MinimapTargetViewModel> _all = new();
    private readonly string _gameFilePath;
    private readonly Action<List<MinimapIconTargets.Target>> _onScanned;
    private readonly DispatcherTimer _searchDebounce;
    private CollectionViewSource? _allView;
    private List<string> _catalog = [];
    private string _query = "";
    private int _shown;

    /// <summary>
    /// Icons worth making obvious: valuable league content and the things people walk past.
    /// Shape carries the meaning, color carries the value, so they read at a glance.
    /// </summary>
    private static readonly (string Vanilla, string Icon)[] Preset =
    [
        ("BestiaryLegendaryBeast", "LootFilterLargeYellowStar"),
        ("BestiaryRareMonster", "LootFilterMediumYellowStar"),
        ("Strongbox", "LootFilterLargeYellowDiamond"),
        ("ExpeditionSpecialRemnant", "LootFilterLargeCyanDiamond"),
        ("ExpeditionBoss", "LootFilterLargeRedTriangle"),
        ("MemoryTear", "LootFilterLargePurpleDiamond"),
        ("SanctumRelicAltar", "LootFilterLargePinkHexagon"),
        ("HarvestSeedCache", "LootFilterMediumGreenSquare"),
        ("DelveMineralVein", "LootFilterMediumCyanRaindrop"),
        ("AbyssCrackActive", "LootFilterLargePurpleCircle"),
        ("Breach", "LootFilterLargePurpleStar"),
        ("Essence", "LootFilterMediumWhiteHexagon"),
        ("Shrine", "LootFilterMediumBlueCross"),
    ];

    public MinimapIconsEditor(
        ObservableCollection<MinimapTargetViewModel> selected,
        string gameFilePath,
        Action<List<MinimapIconTargets.Target>> onScanned)
    {
        InitializeComponent();
        _selected = selected;
        _gameFilePath = gameFilePath;
        _onScanned = onScanned;

        var grouped = new CollectionViewSource { Source = selected };
        grouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MinimapTargetViewModel.Category)));
        SelectedItemsControl.ItemsSource = grouped.View;

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchDebounce.Tick += (s, e) => ApplySearch();

        Loaded += async (s, e) => await LoadAsync();
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_gameFilePath) || !File.Exists(_gameFilePath))
        {
            StatusText.Text = "Select a valid game file first";
            return;
        }

        StatusText.Text = "Scanning object types...";
        try
        {
            var targets = await MinimapIconTargets.LoadAsync(_gameFilePath);
            _catalog = await MinimapIconCatalog.LoadAsync(_gameFilePath);
            _onScanned(targets);

            var listed = _selected.Select(t => t.VanillaIcon).ToHashSet(StringComparer.Ordinal);
            foreach (var target in targets)
                _all.Add(new MinimapTargetViewModel(target) { IsAdded = listed.Contains(target.Icon) });

            _allView = new CollectionViewSource { Source = _all };
            _allView.Filter += Filter;
            AllListBox.ItemsSource = _allView.View;
            StatusText.Text = $"{_all.Count} icons in use";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Scan failed";
            MessageBox.Show($"Could not scan the object types:\n\n{ex.Message}", "Error");
        }
    }

    private void Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is not MinimapTargetViewModel target)
        {
            e.Accepted = false;
            return;
        }
        e.Accepted = _query.Length == 0
            || target.VanillaIcon.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || target.Category.Contains(_query, StringComparison.OrdinalIgnoreCase);
        if (e.Accepted)
            _shown++;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = SearchBox.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void ApplySearch()
    {
        _searchDebounce.Stop();
        if (_allView is null)
            return;

        _query = SearchBox.Text.Trim();
        _shown = 0;
        _allView.View.Refresh();
        StatusText.Text = _query.Length == 0
            ? $"{_all.Count} icons in use"
            : $"{_shown} of {_all.Count} icons in use";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        ApplySearch();
        SearchBox.Focus();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not MinimapTargetViewModel target)
            return;

        var existing = _selected.FirstOrDefault(t => t.VanillaIcon == target.VanillaIcon);
        if (existing is null)
        {
            _selected.Add(new MinimapTargetViewModel(
                new MinimapIconTargets.Target(target.VanillaIcon, target.Category, [.. target.Paths]))
            {
                IsSelected = true,
            });
            target.IsAdded = true;
        }
        else
        {
            _selected.Remove(existing);
            target.IsAdded = false;
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not MinimapTargetViewModel target)
            return;
        _selected.Remove(target);
        var listed = _all.FirstOrDefault(t => t.VanillaIcon == target.VanillaIcon);
        if (listed is not null)
            listed.IsAdded = false;
    }

    private void ChangeIconButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not MinimapTargetViewModel target)
            return;
        if (_catalog.Count == 0)
        {
            MessageBox.Show("The icon list has not loaded yet.", "Please wait");
            return;
        }
        if (IconPicker.Show(_catalog, target.VanillaIcon, this) is string chosen)
            target.ChosenIcon = chosen;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not MinimapTargetViewModel target)
            return;
        target.IsExpanded = !target.IsExpanded;
    }

    private void PathChangeIconButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PathChoiceViewModel path)
            return;
        if (_catalog.Count == 0)
        {
            MessageBox.Show("The icon list has not loaded yet.", "Please wait");
            return;
        }
        if (IconPicker.Show(_catalog, path.EffectiveIcon, this) is string chosen)
            path.OverrideIcon = chosen;
    }

    private void PathResetButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PathChoiceViewModel path)
            return;
        path.OverrideIcon = null;
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        // Only offer what this install actually has, so a removed league does not add dead rows.
        var available = _all.ToDictionary(t => t.VanillaIcon, StringComparer.Ordinal);
        var added = 0;
        foreach (var (vanilla, icon) in Preset)
        {
            if (!available.TryGetValue(vanilla, out var source))
                continue;

            var existing = _selected.FirstOrDefault(t => t.VanillaIcon == vanilla);
            if (existing is not null)
            {
                existing.ChosenIcon = icon;
                existing.IsSelected = true;
                continue;
            }
            _selected.Add(new MinimapTargetViewModel(
                new MinimapIconTargets.Target(vanilla, source.Category, [.. source.Paths]), icon)
            {
                IsSelected = true,
            });
            source.IsAdded = true;
            added++;
        }
        StatusText.Text = _all.Count == 0
            ? "Scan has not finished yet"
            : $"preset applied, {added} added";
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _selected)
            target.IsSelected = true;
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var target in _selected)
            target.IsSelected = false;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        // Back to the vanilla icon, which is also how an applied change gets undone.
        foreach (var target in _selected)
        {
            target.ChosenIcon = target.VanillaIcon;
            foreach (var path in target.PathChoices)
                path.OverrideIcon = null;
        }
    }

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
            DwmSetWindowAttribute(hwndSource.Handle, 20, ref _darkMode, sizeof(int));
    }

    private int _darkMode = 1;

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

    public static bool Show(
        ObservableCollection<MinimapTargetViewModel> selected,
        string gameFilePath,
        Action<List<MinimapIconTargets.Target>> onScanned)
    {
        var dialog = new MinimapIconsEditor(selected, gameFilePath, onScanned);
        if (Application.Current.MainWindow is not null)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        return dialog.ShowDialog() == true;
    }
}
