using Avalonia;

namespace PoeRedux;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
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
