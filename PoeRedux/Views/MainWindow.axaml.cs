using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PoeRedux.ViewModels;
using System.ComponentModel;
using System.Diagnostics;

namespace PoeRedux.Views;

public partial class MainWindow : Window, IWindowServices
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.Window = this;
        };
        Closing += OnClosing;
        Opened += (_, _) => MatchTitleBar();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
                ShowWindowState();
        };
    }

    // Windows 11 draws the title bar in the stage color with light text; older Windows ignore the request
    // and keep their own dark title bar.
    private void MatchTitleBar()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) || TryGetPlatformHandle()?.Handle is not { } hwnd)
            return;
        var caption = 0x000F0C0B; // COLORREF of #0B0C0F
        var text = 0x00ECECEC;
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
        DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int)); // DWMWA_TEXT_COLOR
    }

    // The window draws its own title bar (WindowDecorations="BorderOnly"): Avalonia's drawn title printed over the
    // window's name and its full screen button over Browse.
    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.ClickCount == 1)
            BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!OnControl(e.Source))
            ToggleMaximized();
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void ShowWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Data = Geometry.Parse(maximized ? "M2.5,0.5 H9.5 V7.5 M0.5,2.5 H7.5 V9.5 H0.5 Z" : "M0.5,0.5 H9.5 V9.5 H0.5 Z");
        ToolTip.SetTip(MaximizeButton, maximized ? "Restore" : "Maximize");
    }

    /// <summary>The pointer is on a button, a text box or another control of the title bar, not on its empty part.</summary>
    private static bool OnControl(object? source) =>
        source is Visual visual && visual.GetSelfAndVisualAncestors().Any(v => v is Button or TextBox or RadioButton);

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;
        // Closing now would stop the game index save half way.
        if (vm.IsBusy)
        {
            e.Cancel = true;
            return;
        }
        vm.SaveSettings();
    }

    public async Task<string?> PickGameFileAsync(string? currentPath)
    {
        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(currentPath) && Path.GetDirectoryName(currentPath.Trim()) is { } dir && Directory.Exists(dir))
            start = await StorageProvider.TryGetFolderFromPathAsync(dir);

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the game file",
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter =
            [
                new FilePickerFileType("Content.ggpk or _.index.bin") { Patterns = ["*.ggpk", "*.bin"] },
            ],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public void ShowChanges(ChangesViewModel changes) => new ChangesWindow(changes).Show(this);

    public Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel) =>
        new ConfirmDialog(title, message, confirm, cancel).ShowDialog<bool>(this);

    public void Open(string pathOrUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = pathOrUrl, UseShellExecute = true });
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            // Nothing to open it with; the path or link stays visible in the window.
        }
    }
}
