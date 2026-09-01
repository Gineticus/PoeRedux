using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

using PoeRedux.Models;
using PoeRedux.Patches;

namespace PoeRedux;

public partial class EnvKeysEditor : Window
{
    private readonly ObservableCollection<EnvKeyViewModel> _keys;

    private EnvKeysEditor(IKeyTogglePatch patch)
    {
        InitializeComponent();
        TitleText.Text = PatchViewModel.DisplayName(patch.Name);
        _keys = [.. patch.Keys.Select(k =>
            new EnvKeyViewModel(k.Id, k.Label, k.Description) { IsSelected = !patch.DisabledKeys.Contains(k.Id) })];
        KeysList.ItemsSource = _keys;
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private void AllButton_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void NoneButton_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool selected)
    {
        foreach (var key in _keys)
            key.IsSelected = selected;
    }

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

    /// <summary>Edits the patch in place and returns whether the user saved.</summary>
    public static bool Show(IKeyTogglePatch patch, Window owner)
    {
        var dialog = new EnvKeysEditor(patch)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() != true)
            return false;

        patch.DisabledKeys.Clear();
        foreach (var key in dialog._keys.Where(k => !k.IsSelected))
            patch.DisabledKeys.Add(key.Id);
        return true;
    }
}

public class EnvKeyViewModel(string id, string label, string description) : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string Id { get; } = id;

    public string Label { get; } = label;

    public string Description { get; } = description;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
