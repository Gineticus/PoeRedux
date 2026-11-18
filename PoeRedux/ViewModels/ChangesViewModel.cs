using CommunityToolkit.Mvvm.ComponentModel;
using PoeRedux.Models;
using PoeRedux.Services;
using System.Collections.ObjectModel;

namespace PoeRedux.ViewModels;

/// <summary>One row of the comparison: a removed, added, unchanged or skipped line.</summary>
public sealed record DiffRow(DiffKind Kind, string Text)
{
    public bool IsRemoved => Kind == DiffKind.Removed;
    public bool IsAdded => Kind == DiffKind.Added;
    public bool IsGap => Kind == DiffKind.Gap;

    public string Display => Kind switch
    {
        DiffKind.Removed => "- " + Text,
        DiffKind.Added => "+ " + Text,
        DiffKind.Gap => Text,
        _ => "  " + Text,
    };
}

/// <summary>The changed game files and, for the selected one, its lines before and after.</summary>
public sealed partial class ChangesViewModel : ObservableObject, IDisposable
{
    private readonly string _gamePath;
    private readonly PoeGame _game;
    private ChangeReview? _review;
    private List<string> _all = [];
    private int _request;

    // A list of 50,000 rows is slow to build and useless to scroll; search narrows it.
    private const int ListLimit = 2000;

    public ChangesViewModel(string gamePath, PoeGame game)
    {
        _gamePath = gamePath;
        _game = game;
    }

    public ObservableCollection<string> Paths { get; } = [];
    public ObservableCollection<DiffRow> Lines { get; } = [];

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial string? SelectedPath { get; set; }
    [ObservableProperty] public partial string? Note { get; set; } = "Reading the backup and the game file…";
    [ObservableProperty] public partial string? Count { get; set; }

    partial void OnSearchChanged(string value) => Filter();
    partial void OnSelectedPathChanged(string? value) => _ = CompareAsync(value);

    public async Task LoadAsync()
    {
        try
        {
            _review = await Task.Run(() => ChangeReview.Open(_gamePath, _game));
            _all = [.. _review.Paths];
            Note = _all.Count == 0 ? "PoE Redux has changed no files of this game." : "Choose a file to see its changed lines.";
            Filter();
        }
        catch (Exception e)
        {
            Note = "Could not open the backup or the game file: " + e.Message;
        }
    }

    private void Filter()
    {
        var words = Search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Paths.Clear();
        var matches = _all.Where(p => words.All(w => p.Contains(w, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var path in matches.Take(ListLimit))
            Paths.Add(path);
        var files = _all.Count == 1 ? "1 changed file" : $"{_all.Count:N0} changed files";
        Count = matches.Count > ListLimit
            ? $"{files}; the first {ListLimit:N0} of {matches.Count:N0} matches are listed"
            : words.Length > 0 ? $"{matches.Count:N0} of {files}" : files;
    }

    private async Task CompareAsync(string? path)
    {
        var request = ++_request;
        Lines.Clear();
        if (path is null || _review is not { } review)
            return;
        Note = "Comparing…";
        try
        {
            var lines = await Task.Run(() => review.Compare(path));
            if (request != _request)
                return;
            foreach (var line in lines)
                Lines.Add(new DiffRow(line.Kind, line.Text));
            Note = lines.Count == 0 ? "The file has its original bytes." : null;
        }
        catch (Exception e)
        {
            Note = "Could not compare this file: " + e.Message;
        }
    }

    public void Dispose()
    {
        _review?.Dispose();
        _review = null;
    }
}
