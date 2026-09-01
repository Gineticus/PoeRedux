using PoeRedux.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoeRedux.Models;

public class ColorModsViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _group;

    public ColorModsOption Option { get; }
    public string Name => Option.Name;
    public string Category => Option.Category;
    public IReadOnlyList<string> AvailableGroups => ModColorGroups.Names;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Which color group this mod is in. The group owns the color.</summary>
    public string Group
    {
        get => _group;
        set
        {
            if (_group == value)
                return;
            _group = value;
            OnPropertyChanged();
        }
    }

    public ColorModsViewModel(ColorModsOption option)
    {
        Option = option;
        _isSelected = option.IsEnabled;
        _group = ModColorGroups.Normalize(option.Group);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
