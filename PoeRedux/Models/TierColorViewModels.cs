using PoeRedux.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace PoeRedux.Models;

/// <summary>A color group with its base color, edited as hex.</summary>
public class TierGroupViewModel : INotifyPropertyChanged
{
    private byte _r, _g, _b;
    private string _tag = "";

    public string Name { get; }

    /// <summary>Short label written before the mod in tag mode.</summary>
    public string Tag
    {
        get => _tag;
        set { if (_tag == value) return; _tag = value; OnPropertyChanged(); }
    }

    public byte R { get => _r; set { _r = value; Changed(); } }
    public byte G { get => _g; set { _g = value; Changed(); } }
    public byte B { get => _b; set { _b = value; Changed(); } }

    public string Hex
    {
        get => $"#{_r:X2}{_g:X2}{_b:X2}";
        set
        {
            // Ignore half typed values instead of fighting the user's keystrokes.
            if (!HexColor.TryParse(value, out var r, out var g, out var b))
                return;
            _r = r; _g = g; _b = b;
            Changed();
        }
    }

    public Brush Swatch => new SolidColorBrush(Color.FromRgb(_r, _g, _b));

    public TierGroupViewModel(string name, byte r, byte g, byte b, string tag = "")
    {
        Name = name;
        _r = r; _g = g; _b = b;
        _tag = tag;
    }

    protected void Changed()
    {
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(Swatch));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>One quality band: the color, and the fraction of the ceiling it starts at.</summary>
public class RollTierViewModel : TierGroupViewModel
{
    private double _minFraction;
    private bool _isEnabled;

    public double MinFraction
    {
        get => _minFraction;
        set { if (Math.Abs(_minFraction - value) < 0.0001) return; _minFraction = value; Changed(); }
    }

    /// <summary>Edited as a whole-number percent, which is how anyone thinks about it.</summary>
    public string MinPercent
    {
        get => $"{_minFraction * 100:0}";
        set
        {
            if (!double.TryParse(value.Trim().TrimEnd('%'), out var percent))
                return;
            // Above 100 is meaningful: it is how the "better than anything craftable" band is
            // expressed, so clamping to 100 silently turned it into "exactly the maximum".
            MinFraction = Math.Clamp(percent, 1, 200) / 100.0;
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); }
    }

    public RollTierViewModel(RollTier tier)
        : base(tier.Name, tier.R, tier.G, tier.B, tier.Tag)
    {
        _minFraction = tier.MinFraction;
        _isEnabled = tier.Enabled;
    }

    public RollTier ToTier() => new(Name, MinFraction, R, G, B, Tag, IsEnabled);
}

/// <summary>One stat that can be highlighted, and its craftable ceiling.</summary>
public class TierStatViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string StatId { get; }
    public string Text { get; }
    public int Ceiling { get; }
    public int ModCount { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public TierStatViewModel(string statId, string text, int ceiling, int modCount)
    {
        StatId = statId;
        Text = text;
        Ceiling = ceiling;
        ModCount = modCount;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
