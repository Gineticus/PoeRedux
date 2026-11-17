using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Services;
using System.Collections.ObjectModel;

namespace PoeRedux.ViewModels;

/// <summary>What the view model needs from the window.</summary>
public interface IWindowServices
{
    Task<string?> PickGameFileAsync(string? currentPath);
    Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel);
    void Open(string pathOrUrl);
}

public enum NoticeKind
{
    Success,
    Warning,
    Error,
}

public sealed record Notice(NoticeKind Kind, string Text, string? LogPath = null)
{
    public bool IsSuccess => Kind == NoticeKind.Success;
    public bool IsWarning => Kind == NoticeKind.Warning;
    public bool IsError => Kind == NoticeKind.Error;
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettings _settings = AppSettings.Load();
    private List<IPatch> _patches = [];
    private bool _switchingGame;

    public MainWindowViewModel()
    {
        LoadGame(_settings.Game);
        _ = CheckForUpdateAsync();
    }

    public IWindowServices? Window { get; set; }

    public ObservableCollection<PatchGroupViewModel> Groups { get; } = [];
    private IEnumerable<PatchItemViewModel> Items => Groups.SelectMany(g => g.Items);

    public PoeGame Game { get; private set; }

    public bool IsPoe1
    {
        get => Game == PoeGame.PoE1;
        set { if (value) SwitchGame(PoeGame.PoE1); }
    }

    public bool IsPoe2
    {
        get => Game == PoeGame.PoE2;
        set { if (value) SwitchGame(PoeGame.PoE2); }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathProblem))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RestoreCommand))]
    public partial string GamePath { get; set; } = "";

    [ObservableProperty]
    public partial PatchItemViewModel? Current { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(RestoreCommand), nameof(DeleteBackupCommand), nameof(BrowseCommand))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    public bool IsIdle => !IsBusy;

    [ObservableProperty] public partial string Stage { get; set; } = "";
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial double ProgressMax { get; set; } = 1;
    [ObservableProperty] public partial bool ProgressIsIndeterminate { get; set; }
    [ObservableProperty] public partial Notice? Notice { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreCommand), nameof(DeleteBackupCommand))]
    [NotifyPropertyChangedFor(nameof(BackupText), nameof(HasBackup))]
    public partial int PatchedFiles { get; set; }

    [ObservableProperty] public partial string? UpdateText { get; set; }
    private string? _updateUrl;

    public bool HasBackup => PatchedFiles > 0;

    public string BackupText => PatchedFiles switch
    {
        0 => "No game files changed by PoE Redux",
        1 => "1 game file changed, original backed up",
        _ => $"{PatchedFiles:N0} game files changed, originals backed up",
    };

    public int SelectedCount => Items.Count(i => i.IsSelected);

    public string ApplyText => SelectedCount switch
    {
        0 => "Apply",
        1 => "Apply 1 patch",
        _ => $"Apply {SelectedCount} patches",
    };

    /// <summary>Why the game file cannot be used, or null when it can.</summary>
    public string? PathProblem =>
        GamePath.Trim() is var path && path.Length == 0 ? "Choose Content.ggpk (standalone client) or Bundles2\\_.index.bin (Steam, Epic)."
        : !GameIndex.IsSupported(path) ? "This is not Content.ggpk or _.index.bin."
        : !File.Exists(path) ? "There is no file at this path."
        : null;

    private void SwitchGame(PoeGame game)
    {
        if (game == Game || _switchingGame || IsBusy)
            return;
        SaveSettings();
        LoadGame(game);
    }

    private void LoadGame(PoeGame game)
    {
        _switchingGame = true;
        Game = game;
        _settings.Game = game;
        var saved = _settings.For(game);
        _patches = PatchCatalog.Create(game);
        saved.ApplyTo(_patches);

        foreach (var item in Items)
            item.PropertyChanged -= ItemChanged;
        Groups.Clear();
        foreach (var group in _patches.GroupBy(p => p.Group))
        {
            var items = group.Select(p => new PatchItemViewModel(p, SettingsViewModel.For(p, () => PathProblem is null ? GamePath.Trim() : null))
            {
                IsSelected = saved.Selected?.Contains(p.Id) ?? false,
            }).ToList();
            Groups.Add(new PatchGroupViewModel(PatchGroupViewModel.TitleOf(group.Key), items));
        }
        foreach (var item in Items)
            item.PropertyChanged += ItemChanged;

        GamePath = saved.Path;
        Current = Items.FirstOrDefault();
        Notice = null;
        OnPropertyChanged(nameof(IsPoe1));
        OnPropertyChanged(nameof(IsPoe2));
        OnSelectionChanged();
        RefreshBackup();
        _switchingGame = false;
    }

    partial void OnCurrentChanged(PatchItemViewModel? oldValue, PatchItemViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsCurrent = false;
        if (newValue is not null) newValue.IsCurrent = true;
        if (newValue?.Settings is FoldersViewModel folders)
            _ = folders.LoadSuggestionsAsync();
    }

    private void ItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PatchItemViewModel.IsSelected))
            OnSelectionChanged();
        else if (e.PropertyName == nameof(PatchItemViewModel.IsCurrent) && sender is PatchItemViewModel { IsCurrent: true } item)
            Current = item;
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(ApplyText));
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void RefreshBackup()
    {
        try
        {
            PatchedFiles = BackupStore.HasBackup(Game) ? BackupStore.Open(Game).PatchedCount : 0;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            PatchedFiles = 0;
            Notice = new Notice(NoticeKind.Error, "The backup list cannot be read: " + e.Message);
        }
    }

    public void SaveSettings()
    {
        var saved = _settings.For(Game);
        saved.Path = GamePath.Trim();
        saved.CaptureFrom(_patches, Items.Where(i => i.IsSelected).Select(i => i.Id));
        try { _settings.Save(); }
        catch (IOException) { /* settings are a convenience; never block the user on them */ }
    }

    private bool CanBrowse() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseAsync()
    {
        if (Window is null)
            return;
        if (await Window.PickGameFileAsync(GamePath) is { } path)
            GamePath = path;
    }

    private bool CanApply() => !IsBusy && SelectedCount > 0 && PathProblem is null;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        SaveSettings();
        var patches = Items.Where(i => i.IsSelected).Select(i => i.Patch).ToList();
        var path = GamePath.Trim();
        var game = Game;
        using var log = RunLog.Start("apply", game.ToString());
        log.Line("file: " + path);
        log.Line("patches: " + string.Join(", ", patches.Select(p => p.Id)));
        foreach (var patch in patches.OfType<IPatchSettings>())
        {
            if (patch.SaveSettings() is { } options)
                log.Line($"  {((IPatch)patch).Id}: {options.ToJsonString()}");
        }

        await RunAsync(log, async progress =>
        {
            var result = await Task.Run(() => GameIndex.Use(path, parsePaths: true, index => PatchRunner.Run(index, patches, game, progress)));
            log.Line($"result: checked {result.FilesChecked}, written {result.FilesWritten}, restored {result.FilesRestored}, backed up {result.FilesBackedUp}, replaced by game {result.FilesReplacedByGame}");

            var foundNothing = result.PatchesWithoutFiles
                .Concat(patches.OfType<ColorMods>().Where(c => c.ModsFound == 0 && c.Choices.Values.Any(v => v.Enabled)))
                .Distinct()
                .ToList();
            var applied = patches.Count - foundNothing.Count;
            var text = $"Applied {applied} of {patches.Count} patches: {Files(result.FilesWritten)} changed in the game.";
            if (result.FilesRestored > 0)
                text += $" {Files(result.FilesRestored)} of patches you turned off got their originals back.";
            if (result.FilesReplacedByGame > 0)
                text += $" A game update had replaced {Files(result.FilesReplacedByGame)}; they were patched again from the new version.";
            if (foundNothing.Count > 0)
            {
                log.Line("found nothing: " + string.Join(", ", foundNothing.Select(p => p.Id)));
                text += $" Nothing to change was found for {string.Join(", ", foundNothing.Select(p => p.Name))}. Check that the selected game matches the file; if it does, PoE Redux needs an update.";
                return new Notice(NoticeKind.Warning, text, log.FilePath);
            }
            return new Notice(NoticeKind.Success, text, log.FilePath);
        });
    }

    private bool CanRestore() => !IsBusy && HasBackup && PathProblem is null;

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreAsync()
    {
        var path = GamePath.Trim();
        var game = Game;
        using var log = RunLog.Start("restore", game.ToString());
        log.Line("file: " + path);
        await RunAsync(log, async progress =>
        {
            var result = await Task.Run(() => GameIndex.Use(path, parsePaths: false, index => PatchRunner.Restore(index, game, progress)));
            log.Line($"result: restored {result.FilesRestored}, replaced by game {result.FilesReplacedByGame}");
            var text = $"{Files(result.FilesRestored)} got their originals back. The backup was deleted.";
            if (result.FilesReplacedByGame > 0)
                text += $" {Files(result.FilesReplacedByGame)} were left as they are: a game update had replaced them, and their old originals would not match the new version.";
            return new Notice(NoticeKind.Success, text, log.FilePath);
        });
    }

    private bool CanDeleteBackup() => !IsBusy && HasBackup;

    [RelayCommand(CanExecute = nameof(CanDeleteBackup))]
    private async Task DeleteBackupAsync()
    {
        if (Window is null)
            return;
        var confirmed = await Window.ConfirmAsync(
            "Delete the backup?",
            $"PoE Redux keeps the originals of {Files(PatchedFiles)} it changed. Without them, Restore Original cannot undo the patches; only PackCheck.exe or a file check in Steam can.\n\nDelete the backup only after the game files were checked that way.",
            "Delete backup",
            "Keep backup");
        if (!confirmed)
            return;
        BackupStore.Delete(Game);
        RefreshBackup();
        Notice = new Notice(NoticeKind.Success, "The backup was deleted.");
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    [RelayCommand]
    private void OpenLog()
    {
        if (Notice?.LogPath is { } path && File.Exists(path))
            Window?.Open(path);
    }

    [RelayCommand]
    private void OpenUpdate()
    {
        if (_updateUrl is not null)
            Window?.Open(_updateUrl);
    }

    private async Task RunAsync(RunLog log, Func<IProgress<PatchProgress>, Task<Notice>> work)
    {
        IsBusy = true;
        Notice = null;
        Stage = "Reading the game index…";
        ProgressIsIndeterminate = true;
        Progress = 0;
        var progress = new Progress<PatchProgress>(ShowProgress);
        try
        {
            Notice = await work(progress);
        }
        catch (Exception e)
        {
            log.Line("error: " + e);
            Notice = new Notice(NoticeKind.Error, $"Stopped with an error: {e.Message} Close the game and try again. The log has the details.", log.FilePath);
        }
        finally
        {
            IsBusy = false;
            Stage = "";
            RefreshBackup();
        }
    }

    private void ShowProgress(PatchProgress p)
    {
        ProgressIsIndeterminate = p.Total == 0;
        ProgressMax = Math.Max(p.Total, 1);
        Progress = p.Done;
        Stage = p.Stage switch
        {
            PatchStage.Patching => $"Checking game files: {p.Done:N0} of {p.Total:N0}",
            PatchStage.Writing => $"Writing game files: {p.Done:N0} of {p.Total:N0}",
            PatchStage.Saving => "Saving the game index…",
            _ => "Finding the files to change…",
        };
    }

    private static string Files(int count) => count == 1 ? "1 file" : $"{count:N0} files";

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var update = await GitHubUpdateChecker.CheckForUpdatesAsync("Gineticus", "PoeRedux");
            if (update?.IsUpdateAvailable == true)
            {
                _updateUrl = update.DownloadUrl;
                UpdateText = $"Version {update.LatestVersion?.TrimStart('v', 'V')} is out";
            }
        }
        catch (Exception)
        {
            // No network or GitHub is down: the check is only a convenience.
        }
    }
}
