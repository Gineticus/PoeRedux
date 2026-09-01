using LibBundle3.Nodes;

using PoeRedux.Services;

namespace PoeRedux.Patches;

/// <summary>
/// Compacts the mod description lines that wrap the affix name on an item tooltip. Each rare mod
/// costs a whole extra line of "Prefix Modifier" ceremony above the mod itself; this shortens
/// that line and drops "Master" off a crafted mod's own line, so a six-mod rare loses several
/// lines of pure noise. Every replacement keeps the row's {0} placeholder, so the client still has
/// something to fill in.
/// </summary>
public class ClientStrings : IPatch
{
    public string Name => "Compact Mod Lines";
    public object Description =>
        "Shortens the Prefix/Suffix/Crafted line above each mod on an item tooltip.";

    private static readonly Dictionary<string, string> Replacements = new(StringComparer.Ordinal)
    {
        ["ModDescriptionLinePrefix"] = "P \"{0}\"",
        ["ModDescriptionLineSuffix"] = "S \"{0}\"",
        ["ModDescriptionLineCrafted"] = "Crafted {0}",
    };

    public void Apply(DirectoryNode root)
    {
        var data = NavigateTo(root, "data");
        var table = data?.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "clientstrings.datc64");
        if (table is null)
        {
            PatchReport.FileMissing(ClientStringsTable.Path);
            return;
        }

        var found = ClientStringsTable.Rewrite(table,
            (id, _) => Replacements.TryGetValue(id, out var replacement) ? replacement : null);

        if (found == 0)
            PatchReport.NoMatch(ClientStringsTable.Path, "any of the mod description line ids");
    }

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        var current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null) return null;
            current = next;
        }
        return current;
    }
}
