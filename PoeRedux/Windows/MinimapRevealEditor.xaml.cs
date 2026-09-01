using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using PoeRedux.Patches;
using PoeRedux.Services;

namespace PoeRedux;

public partial class MinimapRevealEditor : Window
{
    private MinimapRevealEditor(Minimap patch)
    {
        InitializeComponent();
        Load(patch.RevealStrength, patch.WalkableHex, patch.WalkableOpacity, patch.EdgeHex, patch.EdgeOpacity);
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    private void Load(double reveal, string walkableHex, double walkableOpacity, string edgeHex, double edgeOpacity)
    {
        RevealSlider.Value = reveal;
        WalkableHexBox.Text = walkableHex;
        WalkableOpacitySlider.Value = walkableOpacity;
        EdgeHexBox.Text = edgeHex;
        EdgeOpacitySlider.Value = edgeOpacity;
        ShowValue(RevealValueText, reveal);
        ShowValue(WalkableOpacityText, walkableOpacity);
        ShowValue(EdgeOpacityText, edgeOpacity);
        ShowSwatch(WalkableSwatch, walkableHex);
        ShowSwatch(EdgeSwatch, edgeHex);
    }

    private void RevealSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ShowValue(RevealValueText, e.NewValue);

    private void WalkableOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ShowValue(WalkableOpacityText, e.NewValue);

    private void EdgeOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ShowValue(EdgeOpacityText, e.NewValue);

    private void WalkableHexBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ShowSwatch(WalkableSwatch, WalkableHexBox.Text);

    private void EdgeHexBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        ShowSwatch(EdgeSwatch, EdgeHexBox.Text);

    private static void ShowValue(System.Windows.Controls.TextBlock target, double value)
    {
        if (target is not null)
            target.Text = value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static void ShowSwatch(System.Windows.Controls.Border target, string hex)
    {
        if (target is null)
            return;
        target.Background = HexColor.TryParse(hex, out var r, out var g, out var b)
            ? new SolidColorBrush(Color.FromRgb(r, g, b))
            : Brushes.Transparent;
    }

    private void DefaultsButton_Click(object sender, RoutedEventArgs e) => Load(
        Minimap.DefaultRevealStrength,
        Minimap.DefaultWalkableHex,
        Minimap.DefaultWalkableOpacity,
        Minimap.DefaultEdgeHex,
        Minimap.DefaultEdgeOpacity);

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
    public static bool Show(Minimap patch, Window owner)
    {
        var dialog = new MinimapRevealEditor(patch)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() != true)
            return false;

        patch.RevealStrength = dialog.RevealSlider.Value;
        patch.WalkableOpacity = dialog.WalkableOpacitySlider.Value;
        patch.EdgeOpacity = dialog.EdgeOpacitySlider.Value;
        // A half typed hex would otherwise be saved and then silently fall back at apply time.
        if (HexColor.TryParse(dialog.WalkableHexBox.Text, out _, out _, out _))
            patch.WalkableHex = dialog.WalkableHexBox.Text.Trim();
        if (HexColor.TryParse(dialog.EdgeHexBox.Text, out _, out _, out _))
            patch.EdgeHex = dialog.EdgeHexBox.Text.Trim();
        return true;
    }
}
