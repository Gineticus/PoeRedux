using LibBundle3.Nodes;
using PoeRedux.Services;
using System.Text;

namespace PoeRedux.Patches;

/// <summary>
/// Restyles the icons the minimap already draws. Every write is a value inside an existing
/// MinimapIcon block, never a new component, which is the one thing the client tolerates:
/// adding a component to a server known entity kills it with Buffer Underflow on zone load.
/// </summary>
public class MinimapIcons : IPatch
{
    public string Name => "Minimap Icons";
    public object Description => "Restyles minimap icons, so valuable things stand out.";

    /// <summary>Object type path to the icon id it should use.</summary>
    public Dictionary<string, string> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static DirectoryNode? NavigateTo(DirectoryNode root, string[] path)
    {
        DirectoryNode current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>()
                .FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    public void Apply(DirectoryNode root)
    {
        foreach (var (path, icon) in Overrides)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                continue;

            var dir = NavigateTo(root, segments[..^1]);
            var file = dir?.Children.OfType<FileNode>()
                .FirstOrDefault(f => f.Name.Equals(segments[^1], StringComparison.OrdinalIgnoreCase));
            if (file is null)
            {
                PatchReport.FileMissing(path);
                continue;
            }

            var record = file.Record;
            // Decoding UTF-16 keeps the BOM as a leading U+FEFF, so re-encoding restores it.
            // Prepending the preamble here would write a second one and the client would refuse.
            var text = Encoding.Unicode.GetString(record.Read().Span);
            if (!MinimapIconTargets.Block.IsMatch(text))
            {
                PatchReport.AnchorMissing(path, "MinimapIcon block");
                continue;
            }

            var patched = MinimapIconTargets.Rewrite(text, icon);
            // Unchanged with the block present means the icon is already the one asked for.
            if (patched == text)
                continue;

            BackupManager.RecordOriginal(record);
            record.Write(Encoding.Unicode.GetBytes(patched));
        }
    }
}
