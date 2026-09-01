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

public partial class TierColorsEditor : Window
{
    private readonly ObservableCollection<RollTierViewModel> _groups;
    private readonly ObservableCollection<TierStatViewModel> _stats;
    private readonly string _gameFilePath;
    private readonly Action<ObservableCollection<TierStatViewModel>>? _afterLoad;
    private readonly DispatcherTimer _searchDebounce;
    private CollectionViewSource? _statsView;
    private string _query = "";
    private int _shown;

    public TierColorsEditor(
        ObservableCollection<RollTierViewModel> groups,
        ObservableCollection<TierStatViewModel> stats,
        StyleOptions style,
        string gameFilePath,
        Action<ObservableCollection<TierStatViewModel>>? afterLoad = null)
    {
        InitializeComponent();
        _groups = groups;
        _stats = stats;
        _gameFilePath = gameFilePath;
        _afterLoad = afterLoad;

        GroupsItemsControl.ItemsSource = _groups;
        PerLadderCheckBox.IsChecked = style.PerLadder;
        PerLadderCheckBox.Checked += (s, e) => style.PerLadder = true;
        PerLadderCheckBox.Unchecked += (s, e) => style.PerLadder = false;
        MinLadderShareBox.Text = $"{style.MinLadderShare * 100:0}";
        MinLadderShareBox.TextChanged += (s, e) =>
        {
            if (double.TryParse(MinLadderShareBox.Text.Trim(), out var percent))
                style.MinLadderShare = Math.Clamp(percent, 0, 100) / 100.0;
        };
        TagPrefixCheckBox.IsChecked = style.TagPrefix;
        TagPrefixCheckBox.Checked += (s, e) => style.TagPrefix = true;
        TagPrefixCheckBox.Unchecked += (s, e) => style.TagPrefix = false;
        CompactTierCheckBox.IsChecked = style.CompactTier;
        CompactTierCheckBox.Checked += (s, e) => style.CompactTier = true;
        CompactTierCheckBox.Unchecked += (s, e) => style.CompactTier = false;

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _searchDebounce.Tick += (s, e) => ApplySearch();

        Loaded += async (s, e) => await LoadStatsAsync();
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private async Task LoadStatsAsync()
    {
        if (_stats.Count > 0)
        {
            Bind();
            return;
        }
        if (string.IsNullOrEmpty(_gameFilePath) || !File.Exists(_gameFilePath))
        {
            StatusText.Text = "Select a valid game file first";
            return;
        }

        StatusText.Text = "Reading the mod table...";
        try
        {
            foreach (var stat in await TierColorConfig.LoadStatsAsync(_gameFilePath))
                _stats.Add(stat);
            // Saved assignments land after the table load, so they win over the pattern defaults.
            _afterLoad?.Invoke(_stats);
            Bind();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed to read the mod table";
            MessageBox.Show($"Could not read the mod tiers:\n\n{ex.Message}", "Error");
        }
    }

    private void Bind()
    {
        _statsView = new CollectionViewSource { Source = _stats };
        _statsView.Filter += Filter;
        StatsListBox.ItemsSource = _statsView.View;
        StatusText.Text = $"{_stats.Count} gradable stats";
    }

    private void Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is not TierStatViewModel stat)
        {
            e.Accepted = false;
            return;
        }
        e.Accepted = _query.Length == 0
            || stat.StatId.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || stat.Text.Contains(_query, StringComparison.OrdinalIgnoreCase);
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
        if (_statsView is null)
            return;
        _query = SearchBox.Text.Trim();
        _shown = 0;
        _statsView.View.Refresh();
        StatusText.Text = _query.Length == 0
            ? $"{_stats.Count} gradable stats"
            : $"{_shown} of {_stats.Count} gradable stats";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        ApplySearch();
        SearchBox.Focus();
    }

    /// <summary>Select or clear only what the current filter shows.</summary>
    private void SetVisible(bool selected)
    {
        if (_statsView?.View is null)
            return;
        foreach (var item in _statsView.View.Cast<TierStatViewModel>().ToList())
            item.IsSelected = selected;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e) => SetVisible(true);

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e) => SetVisible(false);

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

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            int attribute = 20, useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(hwndSource.Handle, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static bool Show(
        ObservableCollection<RollTierViewModel> groups,
        ObservableCollection<TierStatViewModel> stats,
        StyleOptions style,
        string gameFilePath,
        Action<ObservableCollection<TierStatViewModel>>? afterLoad = null)
    {
        var dialog = new TierColorsEditor(groups, stats, style, gameFilePath, afterLoad);
        if (Application.Current.MainWindow is not null)
        {
            dialog.Owner = Application.Current.MainWindow;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        return dialog.ShowDialog() == true;
    }
}
