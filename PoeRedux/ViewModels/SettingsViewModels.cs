using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoeRedux.Patches;
using PoeRedux.Patches.ModColors;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PoeRedux.ViewModels;

/// <summary>The right-hand panel of a patch: its settings, or only its description when it has none.</summary>
public abstract partial class SettingsViewModel : ObservableObject
{
    protected SettingsViewModel(IPatch patch) => Patch = patch;

    public IPatch Patch { get; }
    public string Name => Patch.Name;
    public string Description => Patch.Description;

    public virtual string Summary => "";
    public virtual bool CanReset => Patch is IPatchSettings;

    /// <summary>Raised when a setting changes, so the row summary and the saved settings follow.</summary>
    public event EventHandler? Changed;

    protected void NotifyChanged()
    {
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Reset()
    {
        if (Patch is IPatchSettings settings)
            settings.ResetSettings();
        Reload();
        NotifyChanged();
    }

    /// <summary>Reads every value back from the patch after it changed outside this view model.</summary>
    public abstract void Reload();

    public static SettingsViewModel For(IPatch patch, Func<string?> gamePath) => patch switch
    {
        ColorMods colorMods => new ColorModsViewModel(colorMods),
        Camera camera => new CameraViewModel(camera),
        Minimap minimap => new MinimapViewModel(minimap),
        SwitchPatch switches => new SwitchesViewModel(switches),
        FolderSweepPatch folders => new FoldersViewModel(folders, gamePath),
        _ => new InfoViewModel(patch),
    };
}

public sealed class InfoViewModel(IPatch patch) : SettingsViewModel(patch)
{
    /// <summary>Tooltip lines before and after, shown for Compact Mod Lines only.</summary>
    public string? Before => Sample(Patch.Id).Before;
    public string? After => Sample(Patch.Id).After;

    public bool HasSample => Before is not null;

    private static (string? Before, string? After) Sample(string id) => id switch
    {
        "compact-mod-lines" => ("Prefix Modifier \"Tyrannical\"\nSuffix Modifier \"of Haast\"", "P \"Tyrannical\"\nS \"of Haast\""),
        _ => (null, null),
    };

    public override void Reload() { }
}

public sealed partial class CameraViewModel : SettingsViewModel
{
    private readonly Camera _camera;

    public CameraViewModel(Camera camera) : base(camera)
    {
        _camera = camera;
        Zoom = camera.ZoomLevel;
    }

    public double Min => Camera.MinZoom;
    public double Max => Camera.MaxZoom;
    public override string Summary => $"zoom {_camera.ZoomLevel.ToString("0.0", CultureInfo.InvariantCulture)}";
    public string ZoomText => Zoom.ToString("0.0", CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial double Zoom { get; set; }

    partial void OnZoomChanged(double value)
    {
        _camera.ZoomLevel = value;
        OnPropertyChanged(nameof(ZoomText));
        NotifyChanged();
    }

    public override void Reload() => Zoom = _camera.ZoomLevel;
}

public sealed partial class MinimapViewModel : SettingsViewModel
{
    private readonly Minimap _minimap;
    private bool _loading;

    public MinimapViewModel(Minimap minimap) : base(minimap)
    {
        _minimap = minimap;
        Reload();
    }

    public override string Summary => $"{Math.Round(_minimap.Reveal * 100).ToString(CultureInfo.InvariantCulture)}% reveal";

    [ObservableProperty] public partial double RevealPercent { get; set; }
    [ObservableProperty] public partial Color WalkableColor { get; set; }
    [ObservableProperty] public partial double WalkableOpacityPercent { get; set; }
    [ObservableProperty] public partial Color EdgeColor { get; set; }
    [ObservableProperty] public partial double EdgeOpacityPercent { get; set; }

    public IBrush WalkableBrush => new SolidColorBrush(WalkableColor, WalkableOpacityPercent / 100);
    public IBrush EdgeBrush => new SolidColorBrush(EdgeColor, Math.Min(1, 0.35 + EdgeOpacityPercent / 100 * 6.5));

    /// <summary>How strongly the drawing shows the part of the map nobody has walked.</summary>
    public double UnexploredOpacity => RevealPercent / 100;

    partial void OnRevealPercentChanged(double value) => Push();
    partial void OnWalkableColorChanged(Color value) => Push();
    partial void OnWalkableOpacityPercentChanged(double value) => Push();
    partial void OnEdgeColorChanged(Color value) => Push();
    partial void OnEdgeOpacityPercentChanged(double value) => Push();

    private void Push()
    {
        OnPropertyChanged(nameof(WalkableBrush));
        OnPropertyChanged(nameof(EdgeBrush));
        OnPropertyChanged(nameof(UnexploredOpacity));
        if (_loading)
            return;
        _minimap.Reveal = Math.Round(RevealPercent) / 100;
        _minimap.WalkableColor = new Rgb(WalkableColor.R, WalkableColor.G, WalkableColor.B);
        _minimap.WalkableOpacity = Math.Round(WalkableOpacityPercent) / 100;
        _minimap.EdgeColor = new Rgb(EdgeColor.R, EdgeColor.G, EdgeColor.B);
        _minimap.EdgeOpacity = Math.Round(EdgeOpacityPercent) / 100;
        NotifyChanged();
    }

    public override void Reload()
    {
        _loading = true;
        RevealPercent = Math.Round(_minimap.Reveal * 100);
        WalkableColor = Color.FromRgb(_minimap.WalkableColor.R, _minimap.WalkableColor.G, _minimap.WalkableColor.B);
        WalkableOpacityPercent = Math.Round(_minimap.WalkableOpacity * 100);
        EdgeColor = Color.FromRgb(_minimap.EdgeColor.R, _minimap.EdgeColor.G, _minimap.EdgeColor.B);
        EdgeOpacityPercent = Math.Round(_minimap.EdgeOpacity * 100);
        _loading = false;
    }
}

public sealed partial class SwitchItemViewModel(PatchSwitch patchSwitch, SwitchesViewModel owner) : ObservableObject
{
    public string Name => patchSwitch.Name;
    public string Id => patchSwitch.Id;

    public bool IsOn
    {
        get => !owner.Patch.Off.Contains(patchSwitch.Id);
        set
        {
            if (value == IsOn)
                return;
            if (value) owner.Patch.Off.Remove(patchSwitch.Id);
            else owner.Patch.Off.Add(patchSwitch.Id);
            OnPropertyChanged();
            owner.SwitchChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsOn));
}

public sealed class SwitchesViewModel : SettingsViewModel
{
    public SwitchesViewModel(SwitchPatch patch) : base(patch)
    {
        Patch = patch;
        Switches = patch.Switches.Select(s => new SwitchItemViewModel(s, this)).ToList();
    }

    public new SwitchPatch Patch { get; }
    public IReadOnlyList<SwitchItemViewModel> Switches { get; }
    public override string Summary => $"{Patch.OnSwitches.Count()} of {Patch.Switches.Count}";

    internal void SwitchChanged() => NotifyChanged();

    public override void Reload()
    {
        foreach (var s in Switches)
            s.Refresh();
    }
}

public sealed partial class FoldersViewModel : SettingsViewModel
{
    private readonly Func<string?> _gamePath;
    private string? _loadedFor;

    public FoldersViewModel(FolderSweepPatch patch, Func<string?> gamePath) : base(patch)
    {
        Patch = patch;
        _gamePath = gamePath;
        Reload();
    }

    public new FolderSweepPatch Patch { get; }
    public ObservableCollection<string> Kept { get; } = [];
    public ObservableCollection<string> Suggestions { get; } = [];
    public string Root => Patch.Root + "/";
    public override string Summary => $"{Patch.Kept.Count} kept";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewFolder { get; set; } = "";

    [ObservableProperty] public partial bool IsLoadingFolders { get; set; }
    [ObservableProperty] public partial string? FolderListNote { get; set; }

    private bool CanAdd() => NewFolder.Trim().TrimEnd('/') is var f && f.StartsWith(Root, StringComparison.Ordinal) && f.Length > Root.Length && !Patch.Kept.Contains(f);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        Patch.Kept.Add(NewFolder.Trim().TrimEnd('/'));
        NewFolder = "";
        Reload();
        NotifyChanged();
    }

    [RelayCommand]
    private void Remove(string folder)
    {
        Patch.Kept.Remove(folder);
        Reload();
        NotifyChanged();
    }

    public override void Reload()
    {
        Kept.Clear();
        foreach (var folder in Patch.Kept)
            Kept.Add(folder);
    }

    /// <summary>Reads the folder names from the selected game file, once per file, for the add box.</summary>
    public async Task LoadSuggestionsAsync()
    {
        var path = _gamePath();
        if (path is null || path == _loadedFor || IsLoadingFolders)
            return;
        IsLoadingFolders = true;
        FolderListNote = "Reading folder names from the game file…";
        try
        {
            var root = Patch.Root;
            var folders = await Task.Run(() => Services.GameIndex.ListFolders(path, root));
            Suggestions.Clear();
            foreach (var folder in folders)
                Suggestions.Add(folder);
            _loadedFor = path;
            FolderListNote = null;
        }
        catch (Exception e)
        {
            FolderListNote = "Could not read folder names: " + e.Message;
        }
        finally
        {
            IsLoadingFolders = false;
        }
    }
}
