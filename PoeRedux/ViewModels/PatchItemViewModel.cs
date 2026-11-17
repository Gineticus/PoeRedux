using CommunityToolkit.Mvvm.ComponentModel;
using PoeRedux.Patches;

namespace PoeRedux.ViewModels;

/// <summary>One row of the patch list.</summary>
public partial class PatchItemViewModel : ObservableObject
{
    public PatchItemViewModel(IPatch patch, SettingsViewModel settings)
    {
        Patch = patch;
        Settings = settings;
        settings.Changed += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    public IPatch Patch { get; }
    public SettingsViewModel Settings { get; }
    public string Id => Patch.Id;
    public string Name => Patch.Name;
    public string Description => Patch.Description;

    /// <summary>Short state shown at the end of the row, such as "zoom 2.4".</summary>
    public string Summary => Settings.Summary;

    /// <summary>Whether Apply uses this patch.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Whether this row's settings are shown.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }
}

public sealed class PatchGroupViewModel(string title, IReadOnlyList<PatchItemViewModel> items)
{
    public string Title { get; } = title;
    public IReadOnlyList<PatchItemViewModel> Items { get; } = items;

    public static string TitleOf(PatchGroup group) => group switch
    {
        PatchGroup.Readability => "Readability",
        PatchGroup.MapAndCamera => "Map and camera",
        PatchGroup.VisualNoise => "Visual noise",
        _ => "Experimental",
    };
}
