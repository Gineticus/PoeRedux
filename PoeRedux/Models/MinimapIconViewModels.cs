using PoeRedux.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PoeRedux.Models;

/// <summary>One icon the game ships, and what it should be restyled to.</summary>
public class MinimapTargetViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isAdded;
    private bool _isExpanded;
    private string _chosenIcon;

    /// <summary>The icon this object type uses in an unmodified install.</summary>
    public string VanillaIcon { get; }

    /// <summary>
    /// What to show in the UI: same as <see cref="VanillaIcon"/>, except for an object whose
    /// icon is chosen by the game at runtime, which shows the object's own name instead of the
    /// internal placeholder key.
    /// </summary>
    public string DisplayName => MinimapIconTargets.DisplayNameOf(VanillaIcon);

    public string Category { get; }
    public int FileCount { get; }
    public IReadOnlyList<string> Paths { get; }

    public string ChosenIcon
    {
        get => _chosenIcon;
        set
        {
            if (_chosenIcon == value)
                return;
            _chosenIcon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ChosenGeometry));
            OnPropertyChanged(nameof(ChosenBrush));
            OnPropertyChanged(nameof(ChosenScale));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    /// <summary>True while this icon is in the Selected tab, which flips the All tab's button.</summary>
    public bool IsAdded
    {
        get => _isAdded;
        set
        {
            if (_isAdded == value) return;
            _isAdded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActionLabel));
        }
    }

    public string ActionLabel => _isAdded ? "Remove" : "Add";
    public string FileCountText => FileCount == 1 ? "1 type" : $"{FileCount} types";

    /// <summary>Whether this row groups more than one object type, so it is worth expanding.</summary>
    public bool IsExpandable => FileCount > 1;

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpandGlyph)); }
    }

    public string ExpandGlyph => _isExpanded ? "▾" : "▸";

    /// <summary>One row per object type sharing this icon, so each can be restyled on its own.</summary>
    public ObservableCollection<PathChoiceViewModel> PathChoices { get; }

    public Geometry VanillaGeometry => IconGlyphs.GeometryFor(VanillaIcon);
    public Brush VanillaBrush => IconGlyphs.BrushFor(VanillaIcon);
    public double VanillaScale => IconGlyphs.ScaleFor(VanillaIcon);

    public Geometry ChosenGeometry => IconGlyphs.GeometryFor(_chosenIcon);
    public Brush ChosenBrush => IconGlyphs.BrushFor(_chosenIcon);
    public double ChosenScale => IconGlyphs.ScaleFor(_chosenIcon);

    public MinimapTargetViewModel(
        MinimapIconTargets.Target target,
        string? chosenIcon = null,
        IReadOnlyDictionary<string, string>? pathOverrides = null)
    {
        VanillaIcon = target.Icon;
        Category = target.Category;
        FileCount = target.FileCount;
        Paths = target.Paths;
        _chosenIcon = chosenIcon ?? target.Icon;
        PathChoices = new ObservableCollection<PathChoiceViewModel>(
            target.Paths.Select(p => new PathChoiceViewModel(
                this, p, pathOverrides is not null && pathOverrides.TryGetValue(p, out var o) ? o : null)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>One object type inside a multi-type row, restyled on its own or falling back to the group's icon.</summary>
public class PathChoiceViewModel : INotifyPropertyChanged
{
    private readonly MinimapTargetViewModel _parent;
    private string? _overrideIcon;

    public string Path { get; }
    public string DisplayName { get; }

    /// <summary>Null means "use the group's chosen icon".</summary>
    public string? OverrideIcon
    {
        get => _overrideIcon;
        set
        {
            if (_overrideIcon == value)
                return;
            _overrideIcon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasOverride));
            RaiseEffectiveChanged();
        }
    }

    public bool HasOverride => _overrideIcon is not null;
    public string EffectiveIcon => _overrideIcon ?? _parent.ChosenIcon;

    public Geometry Geometry => IconGlyphs.GeometryFor(EffectiveIcon);
    public Brush Brush => IconGlyphs.BrushFor(EffectiveIcon);
    public double Scale => IconGlyphs.ScaleFor(EffectiveIcon);

    public PathChoiceViewModel(MinimapTargetViewModel parent, string path, string? overrideIcon)
    {
        _parent = parent;
        Path = path;
        DisplayName = System.IO.Path.GetFileNameWithoutExtension(path);
        _overrideIcon = overrideIcon;
        // The group icon only matters while this path has no override of its own.
        parent.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MinimapTargetViewModel.ChosenIcon) && _overrideIcon is null)
                RaiseEffectiveChanged();
        };
    }

    private void RaiseEffectiveChanged()
    {
        OnPropertyChanged(nameof(EffectiveIcon));
        OnPropertyChanged(nameof(Geometry));
        OnPropertyChanged(nameof(Brush));
        OnPropertyChanged(nameof(Scale));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>One choice in the icon picker grid.</summary>
public class IconChoiceViewModel
{
    public string Id { get; }
    public string Size { get; }
    public string Color { get; }
    public string Shape { get; }
    public bool IsNamed { get; }

    public Geometry Geometry => IconGlyphs.GeometryFor(Id);
    public Brush Brush => IconGlyphs.BrushFor(Id);
    public double Scale => IconGlyphs.ScaleFor(Id);

    public IconChoiceViewModel(string id)
    {
        Id = id;
        IsNamed = !MinimapIconCatalog.TryDescribe(id, out var size, out var color, out var shape);
        Size = size;
        Color = color;
        Shape = shape;
    }
}
