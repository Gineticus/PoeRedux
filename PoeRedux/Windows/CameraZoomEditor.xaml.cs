using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

using PoeRedux.Patches;

namespace PoeRedux;

public partial class CameraZoomEditor : Window
{
    private CameraZoomEditor(double zoom)
    {
        InitializeComponent();
        ZoomSlider.Value = zoom;
        ShowValue(zoom);
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
    }

    public double Zoom => ZoomSlider.Value;

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        ShowValue(e.NewValue);

    private void ShowValue(double zoom)
    {
        if (ZoomValueText is not null)
            ZoomValueText.Text = zoom.ToString("F1").Replace(',', '.');
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
    public static bool Show(Camera patch, Window owner)
    {
        var dialog = new CameraZoomEditor(patch.ZoomLevel)
        {
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (dialog.ShowDialog() != true)
            return false;

        patch.ZoomLevel = dialog.Zoom;
        return true;
    }
}
