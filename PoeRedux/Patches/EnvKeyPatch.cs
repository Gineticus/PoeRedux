using System.Text;
using System.Text.RegularExpressions;

using LibBundle3.Nodes;

using PoeRedux.Services;

namespace PoeRedux.Patches;

/// <summary>One switch inside the environment files, and how to turn it off.</summary>
public sealed record EnvKey(string Id, string Label, string Description, Func<string, string> Rewrite)
{
    /// <summary>
    /// A named layer. Misspelling the name is what disables it: the client builds the layers it
    /// recognises and silently ignores the rest, so "fog" becomes "xog".
    /// </summary>
    public static EnvKey Layer(string name, string label, string description) =>
        new(name, label, description, data => data.Replace($"\"{name}\"", $"\"x{name[1..]}\""));

    /// <summary>A numeric intensity, zeroed in place so the layer still builds but renders nothing.</summary>
    public static EnvKey Intensity(string name, string label, string description) =>
        new(name, label, description,
            data => Regex.Replace(data, $"(\"{name}\":\\s*)([^,\r\n}}]+)(,?)", "${1}0.0${3}"));
}

/// <summary>
/// A sweep over every .env file that flips a chosen set of switches. Each key is independent, so
/// one patch row covers what used to be several, and the user picks which of them actually fire.
/// </summary>
public abstract class EnvKeyPatch : IKeyTogglePatch
{
    public abstract string Name { get; }
    public abstract object Description { get; }
    public abstract IReadOnlyList<EnvKey> Keys { get; }

    /// <summary>
    /// Keys the user turned off. Stored as the delta because every key is on by default, so a
    /// key added in a later version starts on rather than silently missing from a saved list.
    /// </summary>
    public HashSet<string> DisabledKeys { get; } = new(StringComparer.Ordinal);

    public IEnumerable<EnvKey> ActiveKeys => Keys.Where(k => !DisabledKeys.Contains(k.Id));

    public void Apply(DirectoryNode root)
    {
        var dir = NavigateTo(root, "metadata", "environmentsettings");
        if (dir is null)
        {
            PatchReport.FileMissing(Scope);
            return;
        }

        var active = ActiveKeys.ToList();
        if (active.Count == 0)
        {
            PatchReport.NoMatch(Scope, "any enabled setting, since every one of them is switched off");
            return;
        }

        var changed = 0;
        foreach (var file in EnvFiles(dir))
            if (TryPatchFile(file, active)) changed++;

        if (changed == 0)
            PatchReport.NoMatch(Scope, string.Join(", ", active.Select(k => $"\"{k.Id}\"")));
    }

    private static bool TryPatchFile(FileNode file, List<EnvKey> keys)
    {
        var record = file.Record;
        var data = Encoding.Unicode.GetString(record.Read().ToArray());
        if (data.Length == 0)
            return false;

        var patched = keys.Aggregate(data, (current, key) => key.Rewrite(current));
        if (patched == data)
            return false;

        var newBytes = Encoding.Unicode.GetBytes(patched);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];

        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
    }

    private static IEnumerable<FileNode> EnvFiles(DirectoryNode dir)
    {
        foreach (var node in dir.Children)
        {
            if (node is DirectoryNode child)
            {
                foreach (var file in EnvFiles(child))
                    yield return file;
            }
            else if (node is FileNode file && file.Name.EndsWith(".env", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
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

    private const string Scope = "metadata/environmentsettings";
}
