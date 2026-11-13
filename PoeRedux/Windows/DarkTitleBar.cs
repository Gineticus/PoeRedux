using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PoeRedux;

internal static class DarkTitleBar
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE, supported on Windows 11 and Windows 10 build 19041+.
    private const int UseImmersiveDarkMode = 20;

    /// <summary>
    /// Gives <paramref name="window"/> a dark title bar once it has a window handle. Call it from the constructor.
    /// </summary>
    public static void Enable(Window window)
    {
        window.SourceInitialized += (s, e) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                int enabled = 1;
                DwmSetWindowAttribute(hwndSource.Handle, UseImmersiveDarkMode, ref enabled, sizeof(int));
            }
        };
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
