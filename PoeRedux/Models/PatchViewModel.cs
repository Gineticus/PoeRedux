using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoeRedux.Patches;

namespace PoeRedux.Models;

public class PatchViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public IPatch Patch { get; }

    /// <summary>
    /// The patch name without the " Patch" every one of them used to carry, which said nothing and
    /// pushed the useful word to the right. Saved selections are matched through
    /// <see cref="DisplayName"/> so an older config still finds its patches.
    /// </summary>
    public string Name => DisplayName(Patch.Name);

    public string Description => Patch.Description?.ToString() ?? string.Empty;

    /// <summary>Which divider the patch sits under, and where that divider goes.</summary>
    public string Category => Group().Name;
    public int CategoryOrder => Group().Order;

    /// <summary>Label of this patch's own editor, or null when it has nothing to configure.</summary>
    public string? ConfigLabel => Patch switch
    {
        ColorMods or ColorMods2 => "Mods Colors",
        ModTierColors => "Tier Colors",
        MinimapIcons => "Minimap Icons",
        Camera => "Zoom",
        Minimap => "Reveal",
        Effects => "Effect Folders",
        Particles => "Particle Folders",
        Mtx => "MTX Folders",
        Light => "Light Sources",
        EnvironmentParticles or EnvironmentParticles2 => "Particle Types",
        PostProcess => "Screen Filters",
        _ => null,
    };

    public bool HasConfig => ConfigLabel is not null;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public PatchViewModel(IPatch patch)
    {
        Patch = patch;
        _isSelected = false;
    }

    public static string DisplayName(string patchName)
    {
        var name = patchName;
        foreach (var suffix in Suffixes)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name = name[..^suffix.Length];
        return name.Trim();
    }

    private static readonly string[] Suffixes =
    [
        " (Experimental)",
        " (experimental)",
        " Patch",
    ];

    private (string Name, int Order) Group() => Patch switch
    {
        ColorMods or ColorMods2 or ModTierColors or ClientStrings or MonsterHP => ("Readability", 0),
        Camera or Minimap or MinimapIcons or AtlasFog => ("Map & Camera", 1),
        EnvironmentParticles or EnvironmentParticles2 or Particles or Effects
            or Mtx or PostProcess or Delirium or Corpse or LoadingScreen => ("Visual Noise", 2),
        Shadow or Light => ("Lighting", 3),
        _ => ("Experimental", 4),
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
