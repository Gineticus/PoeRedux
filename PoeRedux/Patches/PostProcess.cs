using LibBundle3.Nodes;
using System.Text;

using PoeRedux.Services;

namespace PoeRedux.Patches;

/// <summary>
/// Forces off individual full-screen filters in the uber post-process shader. Each is a real
/// cbuffer flag the shader checks with a plain `if( x_enable )`, so turning one off is a one-line
/// text edit, same technique <see cref="Minimap"/> uses on its own shaders. Bloom and chromatic
/// aberration are left alone: bloom has no such flag (always runs) and may double up with
/// <see cref="EnvironmentParticles"/>'s own Bloom switch, and the only chromatic split in this
/// shader belongs to a skill/flask radial blur, not a persistent filter.
/// </summary>
public class PostProcess : IPatch, IKeyTogglePatch
{
    public string Name => "Post-Process Patch";
    public object Description =>
        "Forces off individual screen filters: the vignette darkening the screen edges, depth " +
        "of field blur, and the desaturation that washes the screen out on chill or low life.";

    public IReadOnlyList<EnvKey> Keys { get; } =
    [
        new("vignette_enable", "Vignette", "Darkened screen edges.",
            data => data.Replace("if( vignette_enable )", "if( 0 )", StringComparison.Ordinal)),
        new("dof_enable", "Depth of field", "Background and foreground blur.",
            data => data.Replace("if ( dof_enable )", "if ( 0 )", StringComparison.Ordinal)),
        new("desaturation_enable", "Desaturation", "The screen washing out on chill or low life.",
            data => data.Replace("if( desaturation_enable )", "if( 0 )", StringComparison.Ordinal)),
    ];

    public HashSet<string> DisabledKeys { get; } = new(StringComparer.Ordinal);

    public IEnumerable<EnvKey> ActiveKeys => Keys.Where(k => !DisabledKeys.Contains(k.Id));

    public void Apply(DirectoryNode root)
    {
        var shaders = NavigateTo(root, "shaders");
        if (shaders is null)
        {
            PatchReport.FileMissing("shaders");
            return;
        }

        var file = shaders.Children.OfType<FileNode>()
            .FirstOrDefault(f => f.Name.Equals(ShaderFile, StringComparison.Ordinal));
        if (file is null)
        {
            PatchReport.FileMissing("shaders/" + ShaderFile);
            return;
        }

        var active = ActiveKeys.ToList();
        if (active.Count == 0)
        {
            PatchReport.NoMatch(Scope, "any enabled setting, since every one of them is switched off");
            return;
        }

        var record = file.Record;
        var data = Encoding.ASCII.GetString(record.Read().ToArray());
        var patched = active.Aggregate(data, (current, key) => key.Rewrite(current));
        if (patched == data)
        {
            PatchReport.NoMatch(Scope, string.Join(", ", active.Select(k => $"\"{k.Id}\"")));
            return;
        }

        BackupManager.RecordOriginal(record);
        record.Write(Encoding.ASCII.GetBytes(patched));
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

    private const string ShaderFile = "postprocessuber.hlsl";
    private const string Scope = "shaders/" + ShaderFile;
}
