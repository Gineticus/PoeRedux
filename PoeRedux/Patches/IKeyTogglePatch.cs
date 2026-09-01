namespace PoeRedux.Patches;

/// <summary>
/// A patch made of independent named switches, each on by default, sharing one editor dialog
/// and one config entry keyed by patch name. EnvKeyPatch sweeps a folder of .env files;
/// PostProcess rewrites one named shader instead. Both fit the same UI.
/// </summary>
public interface IKeyTogglePatch : IPatch
{
    IReadOnlyList<EnvKey> Keys { get; }

    HashSet<string> DisabledKeys { get; }
}
