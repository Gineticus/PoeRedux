#if DEBUG
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PoeRedux.Services;
using PoeRedux.ViewModels;
using PoeRedux.Views;

namespace PoeRedux.Debugging;

/// <summary>
/// Renders the main window to PNG files for design review:
/// --render out-dir width height game path patch-id [patch-id ...]
/// Each listed patch is selected and rendered as the current one. Nothing outside out-dir is written.
/// </summary>
internal static class RenderMode
{
    public static void Start(string[] args)
    {
        var outDir = args[1];
        var width = int.Parse(args[2]);
        var height = int.Parse(args[3]);
        var game = args[4];
        var path = args[5];
        var ids = args.Skip(6).ToList();

        Directory.CreateDirectory(outDir);
        AppSettings.FilePath = Path.Combine(outDir, "settings.json");
        BackupStore.BackupDirectory = Path.Combine(outDir, "backups");
        RunLog.LogDirectory = Path.Combine(outDir, "logs");
        var settings = new System.Text.Json.Nodes.JsonObject
        {
            ["game"] = game,
            ["games"] = new System.Text.Json.Nodes.JsonObject
            {
                [game] = new System.Text.Json.Nodes.JsonObject
                {
                    ["path"] = path,
                    ["selected"] = new System.Text.Json.Nodes.JsonArray(ids.Select(i => (System.Text.Json.Nodes.JsonNode?)i).ToArray()),
                },
            },
        };
        File.WriteAllText(AppSettings.FilePath, settings.ToJsonString());

        Program.BuildAvaloniaApp().AfterSetup(_ =>
        {
            Dispatcher.UIThread.Post(async () =>
            {
                var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
                var window = lifetime.MainWindow!;
                window.Width = width;
                window.Height = height;
                var vm = (MainWindowViewModel)window.DataContext!;
                await Task.Delay(800);
                foreach (var id in ids.Count > 0 ? ids : ["first"])
                {
                    var item = vm.Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Id == id);
                    if (item is not null)
                        vm.Current = item;
                    await Task.Delay(500);
                    Save(window, Path.Combine(outDir, $"{game}-{id}-{width}x{height}.png"));
                }
                lifetime.Shutdown();
            });
        }).StartWithClassicDesktopLifetime([]);
    }

    private static void Save(Window window, string file)
    {
        var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(window);
        bitmap.Save(file, new PngBitmapEncoderOptions());
    }
}
#endif
