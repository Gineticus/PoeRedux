using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Patches.ModColors;
using System.Collections.ObjectModel;

namespace PoeRedux.ViewModels;

public sealed partial class ModGroupViewModel : ObservableObject
{
    private static readonly Color Stage = Color.Parse("#0B0C0F");
    private readonly ModColorGroup _group;
    private readonly Action _changed;

    public ModGroupViewModel(ModColorGroup group, Action changed)
    {
        _group = group;
        _changed = changed;
        Color = ToColor(group.Color);
    }

    public string Id => _group.Id;
    public string Name => _group.Id;

    [ObservableProperty]
    public partial Color Color { get; set; }

    [ObservableProperty]
    public partial int Count { get; set; }

    public IBrush Brush => new SolidColorBrush(Color);

    /// <summary>The group color mixed into the stage, as the chip background.</summary>
    public IBrush TintBrush => new SolidColorBrush(Mix(Color, Stage, 0.16));

    public string Hex
    {
        get => _group.Color.Hex;
        set
        {
            if (Rgb.TryParse(value, out var rgb))
                Color = ToColor(rgb);
        }
    }

    partial void OnColorChanged(Color value)
    {
        _group.Color = new Rgb(value.R, value.G, value.B);
        OnPropertyChanged(nameof(Brush));
        OnPropertyChanged(nameof(TintBrush));
        OnPropertyChanged(nameof(Hex));
        _changed();
    }

    public void Reload() => Color = ToColor(_group.Color);

    private static Color ToColor(Rgb rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)(a.R * amount + b.R * (1 - amount)),
        (byte)(a.G * amount + b.G * (1 - amount)),
        (byte)(a.B * amount + b.B * (1 - amount)));
}

public sealed partial class ModLineViewModel : ObservableObject
{
    private static readonly IBrush ModBlue = new SolidColorBrush(Color.Parse("#8888FF"));
    private readonly ColorModsViewModel _owner;

    public ModLineViewModel(ModLine line, ColorModsViewModel owner)
    {
        Line = line;
        _owner = owner;
    }

    public ModLine Line { get; }
    public string Key => Line.Key;
    public string Text => Line.Text;
    public string Sample => Line.Sample;
    public string Category => Line.Category;

    private ModLineChoice Choice => _owner.Patch.Choices[Line.Key];

    public bool IsOn
    {
        get => Choice.Enabled;
        set
        {
            if (value == IsOn)
                return;
            _owner.Patch.Choices[Line.Key] = Choice with { Enabled = value };
            Refresh();
            _owner.LineChanged();
        }
    }

    public ModGroupViewModel Group
    {
        get => _owner.Groups.First(g => g.Id == Choice.Group);
        set
        {
            if (value is null || value.Id == Choice.Group)
                return;
            _owner.Patch.Choices[Line.Key] = new ModLineChoice(true, value.Id);
            Refresh();
            _owner.LineChanged();
        }
    }

    /// <summary>The color the game will print this line in after Apply.</summary>
    public IBrush Brush => IsOn ? Group.Brush : ModBlue;

    public IReadOnlyList<ModGroupViewModel> Groups => _owner.Groups;

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(Group));
        OnPropertyChanged(nameof(Brush));
    }
}

public sealed class ModCategoryViewModel(string title, IReadOnlyList<ModLineViewModel> lines)
{
    public string Title { get; } = title;
    public IReadOnlyList<ModLineViewModel> Lines { get; } = lines;
}

public enum LineFilter
{
    All,
    Colored,
    NotColored,
}

public sealed partial class ColorModsViewModel : SettingsViewModel
{
    private readonly List<ModLineViewModel>? _lines;

    public ColorModsViewModel(ColorMods patch) : base(patch)
    {
        Patch = patch;
        Groups = patch.Groups.Select(g => new ModGroupViewModel(g, GroupChanged)).ToList();
        Lines = _lines = patch.Lines.Select(l => new ModLineViewModel(l, this)).ToList();
        ItemName = patch.Game == PoeGame.PoE2 ? "Waystone" : "Map";
        Rebuild();
    }

    public new ColorMods Patch { get; }
    public IReadOnlyList<ModLineViewModel> Lines { get; }
    public IReadOnlyList<ModGroupViewModel> Groups { get; }
    public ObservableCollection<ModCategoryViewModel> Categories { get; } = [];
    public ObservableCollection<ModLineViewModel> PreviewLines { get; } = [];

    /// <summary>"Map" in PoE 1, "Waystone" in PoE 2: the item the preview imitates.</summary>
    public string ItemName { get; }

    public int TotalLines => Lines.Count;
    public int ColoredLines => Lines.Count(l => l.IsOn);
    public override string Summary => $"{ColoredLines} lines";

    public IReadOnlyList<LineFilter> Filters { get; } = Enum.GetValues<LineFilter>();

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial LineFilter Filter { get; set; }

    [ObservableProperty]
    public partial ModLineViewModel? SelectedLine { get; set; }

    [ObservableProperty]
    public partial bool HasNoMatches { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SelectLine(ModLineViewModel line) => SelectedLine = line;

    partial void OnSearchChanged(string value) => Rebuild();
    partial void OnFilterChanged(LineFilter value) => Rebuild();
    partial void OnSelectedLineChanged(ModLineViewModel? value) => RebuildPreview();

    private void GroupChanged()
    {
        // Groups set their color while this view model is still being built.
        if (_lines is null)
            return;
        foreach (var line in _lines)
            line.Refresh();
        RebuildPreview();
        NotifyChanged();
    }

    internal void LineChanged()
    {
        OnPropertyChanged(nameof(ColoredLines));
        RebuildPreview();
        if (Filter != LineFilter.All)
            Rebuild();
        NotifyChanged();
    }

    private void Rebuild()
    {
        var words = Search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var shown = Lines.Where(l =>
            (Filter == LineFilter.All || l.IsOn == (Filter == LineFilter.Colored))
            && words.All(w => l.Text.Contains(w, StringComparison.OrdinalIgnoreCase) || l.Key.Contains(w, StringComparison.OrdinalIgnoreCase)));
        Categories.Clear();
        foreach (var category in shown.GroupBy(l => l.Category))
            Categories.Add(new ModCategoryViewModel(category.Key, category.ToList()));
        HasNoMatches = Categories.Count == 0;
        RebuildPreview();
    }

    // The tooltip preview: the selected line, then one colored line per group, the way the game prints them.
    private void RebuildPreview()
    {
        foreach (var g in Groups)
            g.Count = Lines.Count(l => l.IsOn && l.Group == g);
        var lines = new List<ModLineViewModel>();
        if (SelectedLine is not null)
            lines.Add(SelectedLine);
        foreach (var group in Groups)
        {
            if (Lines.FirstOrDefault(l => l.IsOn && l.Group == group && !lines.Contains(l)) is { } line)
                lines.Add(line);
        }
        if (Lines.FirstOrDefault(l => !l.IsOn && !lines.Contains(l)) is { } plain)
            lines.Add(plain);
        PreviewLines.Clear();
        foreach (var line in lines.Take(7))
            PreviewLines.Add(line);
    }

    public override void Reload()
    {
        foreach (var group in Groups)
            group.Reload();
        foreach (var line in Lines)
            line.Refresh();
        OnPropertyChanged(nameof(ColoredLines));
        Rebuild();
    }
}
