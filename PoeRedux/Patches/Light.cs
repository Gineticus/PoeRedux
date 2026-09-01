namespace PoeRedux.Patches;

public class Light : EnvKeyPatch
{
    public override string Name => "Light Patch";
    public override object Description => "Disables the default light effect in the game.";

    public override IReadOnlyList<EnvKey> Keys { get; } =
    [
        EnvKey.Layer("directional_light", "Directional light", "Sun and moon light, and the shadows it casts."),
        EnvKey.Layer("player_light", "Player light", "The colored glow that follows your character."),
        EnvKey.Layer("environment_mapping", "Environment mapping", "Cubemap reflections and ambient light off the sky."),
        EnvKey.Layer("global_illumination", "Global illumination", "Ambient occlusion and indirect bounced light."),
    ];
}
