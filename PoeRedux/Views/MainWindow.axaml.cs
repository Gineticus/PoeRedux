using Avalonia.Controls;
using Avalonia.Platform.Storage;
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
    }

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
