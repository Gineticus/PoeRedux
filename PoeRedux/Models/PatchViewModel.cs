using System.ComponentModel;
using System.Runtime.CompilerServices;
using PoeRedux.Patches;

namespace PoeRedux.Models;

public class PatchViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public IPatch Patch { get; }
    public string Name => Patch.Name;
    public string Description => Patch.Description?.ToString() ?? string.Empty;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
