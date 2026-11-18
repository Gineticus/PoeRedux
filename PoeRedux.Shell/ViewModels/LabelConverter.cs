using Avalonia.Data.Converters;
using System.Globalization;

namespace PoeRedux.ViewModels;

/// <summary>Words for enum values shown in the window.</summary>
public sealed class LabelConverter : IValueConverter
{
    public static LabelConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LineFilter.All => "All lines",
        LineFilter.Colored => "Colored",
        LineFilter.NotColored => "Not colored",
        _ => value?.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
