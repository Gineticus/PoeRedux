using Avalonia;

namespace PoeRedux;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // The window is in English: numbers and percentages follow it, whatever the Windows language.
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

#if DEBUG
        // Debug builds only: render the window to a PNG and exit, with settings and backups in a temporary folder.
        if (args.Length > 0 && args[0] == "--render")
        {
            Debugging.RenderMode.Start(args);
            return;
        }
#endif
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the XAML previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
