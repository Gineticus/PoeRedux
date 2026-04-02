namespace PoeRedux.Models;

public class ColorModsOption 
{
    public string Name { get; set; }
    public string Color { get; set; }
    public bool IsEnabled { get; set; }

    public ColorModsOption(string name, string color, bool isEnabled)
    {
        Name = name;
        Color = color;
        IsEnabled = isEnabled;
    }

    public ColorModsOption Copy()
    {
        return new ColorModsOption(Name, Color, IsEnabled);
    }
}