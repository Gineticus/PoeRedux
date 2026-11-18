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
                    // "@" ids set a window state instead of choosing a patch.
                    switch (id)
                    {
                        case "@success":
                            vm.Notice = new Notice(NoticeKind.Success, "Applied 7 patches: 50,732 files changed in the game.", "log");
                            break;
                        case "@warning":
                            vm.Notice = new Notice(NoticeKind.Warning, "Applied 6 of 7 patches: 412 files changed in the game. Nothing to change was found for Atlas Fog. Check that the selected game matches the file; if it does, PoE Redux needs an update.", "log");
                            break;
                        case "@error":
                            vm.Notice = new Notice(NoticeKind.Error, "Stopped with an error: The process cannot access the file because it is being used by another process. If the game is running, close it and try again. The log has the details.", "log");
                            break;
                        case "@busy":
                            vm.Notice = null;
                            vm.IsBusy = true;
                            vm.Stage = "Checking game files: 21,504 of 65,145";
                            vm.ProgressMax = 65145;
                            vm.Progress = 21504;
                            break;
                        case "@confirm":
                            vm.IsBusy = false;
                            var dialog = new ConfirmDialog("Delete the backup?", "The backup holds the originals of 50,732 files that PoE Redux changed. Without it, Restore Original cannot undo the patches. On Steam, Verify integrity of game files can still bring the originals back; PackCheck.exe in the standalone client does not.", "Delete backup", "Keep backup");
                            dialog.Show(window);
                            await Task.Delay(700);
                            Save(dialog, Path.Combine(outDir, $"{game}-confirm.png"));
                            dialog.Close();
                            continue;
                        default:
                            if (vm.Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Id == id) is { } item)
                                vm.Current = item;
                            break;
                    }
                    await Task.Delay(500);
                    Save(window, Path.Combine(outDir, $"{game}-{id.TrimStart('@')}-{width}x{height}.png"));
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
