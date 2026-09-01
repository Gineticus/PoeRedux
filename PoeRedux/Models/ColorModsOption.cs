namespace PoeRedux.Models;

public class ColorModsOption
{
    public string Name { get; set; }
    /// <summary>Which color group this mod belongs to. The group carries the color.</summary>
    public string Group { get; set; }
    public bool IsEnabled { get; set; }
    /// <summary>Where the mod shows up in game, used to group the editor list.</summary>
    public string Category { get; set; }

    public ColorModsOption(string name, string group, bool isEnabled, string category = "Other")
    {
        Name = name;
        Group = group;
        IsEnabled = isEnabled;
        Category = category;
    }

    public ColorModsOption Copy()
    {
        return new ColorModsOption(Name, Group, IsEnabled, Category);
    }
}
