using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PoeRedux.Models;

/// <summary>One colorable stat id from the game data, shown in the All Mods tab.</summary>
public class StatCatalogEntry : INotifyPropertyChanged
{
    private bool _isAdded;
    private string _selectedCategory;

    public string Id { get; }
    public string Text { get; }
    public string Category { get; }

    /// <summary>Categories offered in the dropdown, filled in when the catalog loads.</summary>
    public IReadOnlyList<string> AvailableCategories { get; set; } = [];

    /// <summary>The category this stat joins when added, defaulting to where it is described.</summary>
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (_selectedCategory == value) return; _selectedCategory = value; OnPropertyChanged(); }
    }

    /// <summary>True while this id is present in the curated list.</summary>
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

    public StatCatalogEntry(string id, string text, string category)
    {
        Id = id;
        Text = text;
        Category = category;
        _selectedCategory = category;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
