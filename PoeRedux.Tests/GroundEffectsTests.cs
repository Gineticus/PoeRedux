using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Services;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace PoeRedux.Tests;

public class GroundEffectsTests
{
    private const string Fire = "metadata/effects/spells/ground_effects/fire/fire.ao";
    private const string Base = "metadata/effects/spells/ground_effects/base.ao";
    private const string Embers = "metadata/effects/spells/monsters_effects/burning/embers.ao";
    private const string Flames = "metadata/particles/ground_effects/fire/flames.pet";
    private const string EmberParticles = "metadata/particles/monster_effects/embers.pet";
    private const string Fireball = "metadata/effects/spells/fireball/fireball.ao";
    private const string Trail = "metadata/particles/fireball/trail.pet";

    private static readonly string[] Ground = [Fire, Base, Embers, Flames, EmberParticles];

    // A table whose rows hold one string each: a row count, 8-byte string offsets, 8 x 0xBB, UTF-16 strings.
    private static byte[] Table(params string[] strings)
    {
        var data = new MemoryStream();
        data.Write(Enumerable.Repeat((byte)0xBB, 8).ToArray());
        var offsets = new List<long>();
        foreach (var s in strings)
        {
            offsets.Add(data.Length);
            data.Write(Encoding.Unicode.GetBytes(s));
            data.Write(new byte[4]);
        }
        var table = new MemoryStream();
        table.Write(BitConverter.GetBytes(strings.Length));
        foreach (var offset in offsets)
            table.Write(BitConverter.GetBytes(offset));
        table.Write(data.ToArray());
        return table.ToArray();
    }

    private static byte[] Ao(string extends, string blocks) => SyntheticGame.Utf16(
        $"version 3\r\nextends \"{extends}\"\r\n\r\nclient\r\n{{\r\n{blocks}\r\n}}\r\n");

    private static string Particles(string pet) =>
        $"\tParticleEffects\r\n\t{{\r\n\t\tanimations = '[ {{ \"name\": \"loop\", \"events\": [ {{ \"filename\": \"{pet}\" }} ] }} ]'\r\n\t}}";

    // The burning ground extends a parent, attaches embers from another folder, and starts particles.
    private static Dictionary<string, byte[]> Files() => new()
    {
        ["data/groundeffects.datc64"] = Table("Fire", "Metadata/Effects/Spells/ground_effects/fire/fire.ao"),
        [Fire] = Ao("Metadata/Effects/Spells/ground_effects/base",
            "\tSkinMesh\r\n\t{\r\n\t\tskin = \"Art/Models/Effects/fire.sm\"\r\n\t}\r\n" +
            Particles("Metadata/Particles/ground_effects/fire/flames.pet") + "\r\n" +
            "\tAttachedAnimatedObject\r\n\t{\r\n\t\tao = \"Metadata/Effects/Spells/monsters_effects/burning/embers.ao\"\r\n\t}"),
        [Base] = Ao("Metadata/Parent", "\tLights\r\n\t{\r\n\t\tlight = 1\r\n\t}"),
        [Embers] = Ao("Metadata/Parent", Particles("Metadata/Particles/monster_effects/embers.pet")),
        [Fireball] = Ao("Metadata/Parent", Particles("Metadata/Particles/fireball/trail.pet")),
        [Flames] = SyntheticGame.Utf16("version 5\r\n0\r\n{ \"gpu_emitters\": [] }"),
        [EmberParticles] = SyntheticGame.Utf16("version 5\r\n0\r\n{ \"gpu_emitters\": [] }"),
        [Trail] = SyntheticGame.Utf16("version 5\r\n0\r\n{ \"gpu_emitters\": [] }"),
    };

    private static IPatch[] Sweeps(bool keepGround) =>
    [
        new Particles { KeepGroundEffects = keepGround },
        new Effects { KeepGroundEffects = keepGround },
    ];

    private static PatchResult Apply(SyntheticGame game, IPatch[] patches) =>
        game.Use(index => PatchRunner.Run(index, patches, PoeGame.PoE1));

    [Fact]
    public void TablePaths_AreTheAoStringsLowercased()
    {
        var table = Table("Fire", "Metadata/Effects/Spells/ground_effects/fire/fire.ao", "Art/fire.sm", "Metadata/Effects/Spells/x/Y.ao");
        Assert.Equal(
            ["metadata/effects/spells/ground_effects/fire/fire.ao", "metadata/effects/spells/x/y.ao"],
            GroundEffects.AoPaths(table).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Sweeps_LeaveEveryFileAGroundEffectUses()
    {
        var original = Files();
        using var game = new SyntheticGame(original);

        Apply(game, Sweeps(keepGround: true));

        var files = game.ReadAll();
        foreach (var path in Ground)
            Assert.Equal(original[path], files[path]);
        Assert.DoesNotContain("ParticleEffects", SyntheticGame.Text(files[Fireball]));
        Assert.Equal("0", SyntheticGame.Text(files[Trail]));
    }

    [Fact]
    public void GroundEffects_AreFoundInTheOriginalsAfterAnEarlierApplyStrippedThem()
    {
        var original = Files();
        using var game = new SyntheticGame(original);
        Apply(game, Sweeps(keepGround: false));
        Assert.DoesNotContain("ParticleEffects", SyntheticGame.Text(game.ReadAll()[Fire]));

        var result = Apply(game, Sweeps(keepGround: true));

        // fire.ao in the game no longer names its particles; only its backed-up original does.
        Assert.Equal(Ground.Length, result.FilesRestored);
        var files = game.ReadAll();
        foreach (var path in Ground)
            Assert.Equal(original[path], files[path]);
        Assert.Equal("0", SyntheticGame.Text(files[Trail]));
    }

    [Fact]
    public void KeepGroundEffects_IsSavedOnlyWhenTurnedOff()
    {
        var effects = new Effects();
        Assert.True(effects.KeepGroundEffects);
        Assert.Null(effects.SaveSettings());

        effects.KeepGroundEffects = false;
        var saved = effects.SaveSettings()!;
        Assert.False(saved["groundEffects"]!.GetValue<bool>());

        var loaded = new Effects();
        loaded.LoadSettings(saved);
        Assert.False(loaded.KeepGroundEffects);
        loaded.ResetSettings();
        Assert.True(loaded.KeepGroundEffects);
        loaded.LoadSettings(new JsonObject());
        Assert.True(loaded.KeepGroundEffects);
    }
}
