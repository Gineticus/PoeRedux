using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

using PoeRedux.Services;

namespace PoeRedux;

/// <summary>
/// Diffs the current backup's originals against the live game files, so an Apply's changes are
/// reviewable after the fact instead of before it. No dry run: every patch here computes and
/// writes in the same step, so a true pre-write preview would mean refactoring every patch class.
/// This reuses data both sides of the change already produce: the CDN-seeded backup and the file
/// as it stands now.
/// </summary>
public partial class ChangesWindow : Window
{
    private readonly PoeGame _game;
    private readonly string _ggpkPath;

    public ChangesWindow(PoeGame game, string ggpkPath)
    {
        InitializeComponent();
        _game = game;
        _ggpkPath = ggpkPath;
        SourceInitialized += (s, e) => ApplyDarkTitleBar();
        Loaded += (s, e) => LoadDiff();
    }

    private void LoadDiff()
    {
        DiffText.Text = "Reading changes...";
        try
        {
            var originals = BackupManager.EnumerateBackedUp(_game).ToList();
            if (originals.Count == 0)
            {
                DiffText.Text = "No backup for this game.";
                return;
            }

            var sb = new StringBuilder();
            var filesChanged = 0;

            GameFile.Read(_ggpkPath, index =>
            {
                foreach (var (path, original) in originals)
                {
                    if (!index.TryGetFile(path, out var file))
                    {
                        filesChanged++;
                        sb.AppendLine($"=== {path} ===");
                        sb.AppendLine("(missing from the live game files)");
                        sb.AppendLine();
                        continue;
                    }

                    var current = file.Read().ToArray();
                    if (current.AsSpan().SequenceEqual(original)) continue;

                    filesChanged++;
                    sb.AppendLine($"=== {path} ===");
                    sb.Append(ChangeDiff.Format(original, current));
                    sb.AppendLine();
                }
                return 0;
            });

            HeaderText.Text = $"Changes ({filesChanged} file{(filesChanged == 1 ? "" : "s")} differ from backup)";
            DiffText.Text = filesChanged == 0
                ? "No changes: every backed up file matches its current bytes."
                : sb.ToString();
        }
        catch (Exception ex)
        {
            DiffText.Text = "Could not read changes:\n\n" + ex.Message;
        }
    }

    private void ApplyDarkTitleBar()
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            int attribute = 20, useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(hwndSource.Handle, attribute, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void Show(PoeGame game, string ggpkPath, Window owner)
    {
        var dialog = new ChangesWindow(game, ggpkPath);
        if (Application.Current.MainWindow is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        dialog.ShowDialog();
    }
}
