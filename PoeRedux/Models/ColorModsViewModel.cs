using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoeRedux.Models;

public class ColorModsViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    private string _selectedColor;

    public ColorModsOption Option { get; }
    public string Name => Option.Name;

    public List<string> AvailableColors { get; } = new() { "red", "green", "blue", "yellow", "pink" };

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

    public string SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                OnPropertyChanged();
            }
        }
    }

    public ColorModsViewModel(ColorModsOption option)
    {
        Option = option;
        _isSelected = option.IsEnabled;
        _selectedColor = option.Color;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
