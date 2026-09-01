using PoeRedux.Models;
using PoeRedux.Services;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace PoeRedux;

public partial class IconPicker : Window
{
    private const string Any = "Any";

    private readonly List<IconChoiceViewModel> _glyphs;
    private readonly List<IconChoiceViewModel> _named;

    public string? Chosen { get; private set; }

    public IconPicker(IReadOnlyList<string> catalog, string forTarget)
    {
        InitializeComponent();
        TitleText.Text = $"Icon for {forTarget}";

        var choices = catalog.Select(id => new IconChoiceViewModel(id)).ToList();
        _glyphs = choices.Where(c => !c.IsNamed).ToList();
        _named = choices.Where(c => c.IsNamed).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

        ColorFilter.ItemsSource = Options(IconGlyphs.Colors);
        ShapeFilter.ItemsSource = Options(IconGlyphs.Shapes);
        SizeFilter.ItemsSource = Options(["Large", "Medium", "Small"]);
        ColorFilter.SelectedIndex = ShapeFilter.SelectedIndex = SizeFilter.SelectedIndex = 0;

        NamedItemsControl.ItemsSource = _named;
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
        ApplyFilters();
    }

    private static List<string> Options(IReadOnlyList<string> values) => [Any, .. values];

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        if (GlyphItemsControl is null)
            return;

        var color = ColorFilter.SelectedItem as string ?? Any;
        var shape = ShapeFilter.SelectedItem as string ?? Any;
        var size = SizeFilter.SelectedItem as string ?? Any;

        var shown = _glyphs
            .Where(g => color == Any || g.Color == color)
            .Where(g => shape == Any || g.Shape == shape)
            .Where(g => size == Any || g.Size == size)
            .ToList();

        GlyphItemsControl.ItemsSource = shown;
        StatusText.Text = $"{shown.Count} of {_glyphs.Count} shapes, {_named.Count} named icons";
    }

    private void Choose_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not IconChoiceViewModel choice)
            return;
        Chosen = choice.Id;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
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

    /// <summary>Returns the chosen icon id, or null when the user cancelled.</summary>
    public static string? Show(IReadOnlyList<string> catalog, string forTarget, Window owner)
    {
        var dialog = new IconPicker(catalog, forTarget)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        return dialog.ShowDialog() == true ? dialog.Chosen : null;
    }
}
