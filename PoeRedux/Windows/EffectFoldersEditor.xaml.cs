using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

using PoeRedux.Models;
using PoeRedux.Services;

namespace PoeRedux;

public partial class EffectFoldersEditor : Window
{
    private readonly List<EffectSweepViewModel> _sweeps;
    private readonly string _gameFilePath;

    private EffectFoldersEditor(List<EffectSweepViewModel> sweeps, string gameFilePath, int startTab)
    {
        InitializeComponent();
        _sweeps = sweeps;
        _gameFilePath = gameFilePath;
        SweepTabs.ItemsSource = sweeps;
        SweepTabs.SelectedIndex = startTab;
        Loaded += async (s, e) => await LoadTreesAsync();
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    /// <summary>One scan feeds both tabs, since the trees come out of the same index pass.</summary>
    private async Task LoadTreesAsync()
    {
        try
        {
            var trees = await EffectTargets.LoadAsync(_gameFilePath,
                [.. _sweeps.Select(s => new EffectTargets.Sweep(s.Sweep.Root, s.Header, s.Sweep.Extensions))]);

            foreach (var (sweep, tree) in _sweeps.Zip(trees))
                sweep.Load(tree.Folder);
        }
        catch (Exception ex)
        {
            foreach (var sweep in _sweeps)
                sweep.Fail("Could not read the game files.\n\n" + ex.Message);
        }
    }

    private EffectSweepViewModel? Active => SweepTabs.SelectedItem as EffectSweepViewModel;

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Both tabs filter, so switching tabs shows the same search rather than a stale tree.
        var query = MonsterNames.Resolve(SearchBox.Text);
        foreach (var sweep in _sweeps)
            sweep.Filter(query);
    }

    private void AllButton_Click(object sender, RoutedEventArgs e) => Active?.SetAll(true);

    private void NoneButton_Click(object sender, RoutedEventArgs e) => Active?.SetAll(false);

    private void DefaultsButton_Click(object sender, RoutedEventArgs e) => Active?.Reset();

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
            DwmSetWindowAttribute(hwndSource.Handle, 20, ref _darkMode, sizeof(int));
    }

    private int _darkMode = 1;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    /// <summary>Edits every patch in place and returns whether the user saved.</summary>
    public static bool Show(List<EffectSweepViewModel> sweeps, string gameFilePath, int startTab, Window owner)
    {
        var dialog = new EffectFoldersEditor(sweeps, gameFilePath, startTab)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() != true)
            return false;

        foreach (var sweep in sweeps)
            sweep.Save();
        return true;
    }
}
