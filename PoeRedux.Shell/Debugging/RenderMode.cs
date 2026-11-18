#if DEBUG
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PoeRedux.Services;
using PoeRedux.ViewModels;
using PoeRedux.Views;

namespace PoeRedux.Debugging;

/// <summary>
/// Renders the main window to PNG files to check the layout:
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
        BackupStore.BackupDirectory = System.Environment.GetEnvironmentVariable("POEREDUX_RENDER_BACKUPS") ?? Path.Combine(outDir, "backups");
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

        // Popups drawn inside the window, so a window capture includes an open flyout.
        Program.BuildAvaloniaApp().With(new Win32PlatformOptions { OverlayPopups = true }).AfterSetup(_ =>
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
                            vm.PatchedFiles = 50732;
                            vm.Notice = new Notice(NoticeKind.Success, $"Applied {vm.SelectedCount} patches: 50,732 files changed in the game.", "log");
                            break;
                        case "@warning":
                            vm.PatchedFiles = 50732;
                            vm.Notice = new Notice(NoticeKind.Warning, $"Applied {vm.SelectedCount - 1} of {vm.SelectedCount} patches: 50,732 files changed in the game. Nothing to change was found for Color Mods. Check that the selected game matches the file; if it does, PoE Redux needs an update.", "log");
                            break;
                        case "@error":
                            vm.PatchedFiles = 0;
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
                        case "@flyout":
                            // Opens the group picker of the second mod line in Color Mods.
                            var groupButton = window.GetVisualDescendants().OfType<Button>()
                                .Where(b => b.Flyout is Flyout && b.DataContext is ModLineViewModel).Skip(1).FirstOrDefault();
                            groupButton?.Flyout?.ShowAt(groupButton);
                            await Task.Delay(800);
                            Save(window, Path.Combine(outDir, $"{game}-flyout-{width}x{height}.png"));
                            groupButton?.Flyout?.Hide();
                            continue;
                        case "@groupcolor":
                            // Opens the color flyout of the first Color Mods group and captures each tab.
                            var chip = window.GetVisualDescendants().OfType<Button>()
                                .FirstOrDefault(b => b.Flyout is Flyout && b.DataContext is ModGroupViewModel);
                            if (chip?.Flyout is { } chipFlyout)
                                await CaptureColorTabs(window, () => chipFlyout.ShowAt(chip), chipFlyout.Hide, Path.Combine(outDir, $"{game}-groupcolor-{width}x{height}"));
                            continue;
                        case "@colorpicker":
                            // Opens the first ColorPicker of the current settings panel and captures each tab.
                            var picker = window.GetVisualDescendants().OfType<ColorPicker>().FirstOrDefault();
                            var dropDown = picker?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Flyout is not null);
                            if (dropDown?.Flyout is { } pickerFlyout)
                                await CaptureColorTabs(window, () => pickerFlyout.ShowAt(dropDown), pickerFlyout.Hide, Path.Combine(outDir, $"{game}-colorpicker-{width}x{height}"), picker);
                            continue;
                        case "@changes":
                            var changes = new ChangesViewModel(path, vm.Game);
                            var changesWindow = new ChangesWindow(changes);
                            changesWindow.Show(window);
                            await Task.Delay(8000);
                            changes.Search = "environmentsettings";
                            await Task.Delay(500);
                            changes.SelectedPath = changes.Paths.FirstOrDefault();
                            await Task.Delay(2000);
                            Save(changesWindow, Path.Combine(outDir, $"{game}-changes.png"));
                            changesWindow.Close();
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

    // A ColorPicker owns the tabs of its own flyout; a ColorView placed in a flyout is found once it is open.
    private static async Task CaptureColorTabs(Window window, Action show, Action hide, string filePrefix, ColorView? colorView = null)
    {
        show();
        await Task.Delay(800);
        colorView ??= window.GetVisualDescendants().OfType<ColorView>().FirstOrDefault(v => v is not ColorPicker);
        for (var tab = 0; tab < 3; tab++)
        {
            if (colorView is null)
                break;
            if (tab == 1 && !colorView.IsColorPaletteVisible)
                continue;
            colorView.SelectedIndex = tab;
            await Task.Delay(500);
            Save(window, $"{filePrefix}-tab{tab}.png");
        }
        hide();
        await Task.Delay(300);
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
