namespace PoeRedux.Patches;

public class EnvironmentParticles : EnvKeyPatch
{
    public override string Name => "Environment Particles Patch";

    public override object Description =>
        "Disables the default environment particles in the game. Fog is one of these.";

    public override IReadOnlyList<EnvKey> Keys { get; } =
    [
        EnvKey.Layer("area", "Ground and weather", "Ground detail, and weather stuck to the player such as rain."),
        EnvKey.Layer("fog", "Fog", "Distance fog: the tint that thickens further from the camera."),
        EnvKey.Layer("screenspace_fog", "Screen space fog", "Layered swirling fog drawn in front of the camera."),
        EnvKey.Layer("effect_spawner", "Effect spawners", "Ambient effects a zone keeps spawning, drifting souls and embers."),
        EnvKey.Layer("post_processing", "Bloom", "The glow bright pixels bleed onto their surroundings."),
    ];
}
