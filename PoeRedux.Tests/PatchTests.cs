using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Patches.ModColors;
using PoeRedux.Services;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace PoeRedux.Tests;

public class PatchTests
{
    // The two statements as PoE 1 ships them (shaders/minimap_blending_pixel.hlsl, 18 Nov 2026).
    private const string Blending =
        "\tfloat4 walkable_color = float4(1.0f, 1.0f, 1.0f, 0.01f);\r\n" +
        "\tfloat4 walkability_map_color = lerp(walkable_color, float4(0.5f, 0.5f, 1.0f, 0.5f), walkable_to_edge_ratio);\r\n";

    private const string Visibility =
        "\tif (visibility_fully_revealed > 0.5f)\r\n" +
        "\t\tres_color = float4(1.0f, 0.0f, 0.0f, 1.0f);\r\n" +
        "\treturn res_color;\r\n";

    [Fact]
    public void Minimap_DefaultsWriteWhatEarlierVersionsWrote()
    {
        var blending = Minimap.Recolor(Blending, "float4(0.0f, 0.0f, 0.0f, 0.3f)", "float4(12.0f, 12.0f, 12.0f, 0.1f)");
        Assert.Equal(
            "\tfloat4 walkable_color = float4(0.0f, 0.0f, 0.0f, 0.3f);\r\n" +
            "\tfloat4 walkability_map_color = lerp(walkable_color, float4(12.0f, 12.0f, 12.0f, 0.1f), walkable_to_edge_ratio);\r\n",
            blending);

        var visibility = Minimap.RevealFloor(Visibility, "0.18");
        Assert.Contains("res_color = float4(1.0f, 0.0f, 0.0f, 1.0f);\r\n\tres_color = max(res_color, 0.18f);\r\n", visibility);
    }

    [Fact]
    public void Minimap_RewritesItsOwnOutputInsteadOfStacking()
    {
        var once = Minimap.RevealFloor(Visibility, "0.18")!;
        var twice = Minimap.RevealFloor(once, "0.5")!;
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(twice, "max\\(res_color"));
        Assert.Contains("max(res_color, 0.5f)", twice);
    }

    [Fact]
    public void Minimap_FindsNothingInAnUnknownShader()
    {
        Assert.Null(Minimap.Recolor("float4 x = 1;", "a", "b"));
        Assert.Null(Minimap.RevealFloor("return 0;", "0.18"));
    }

    [Fact]
    public void Minimap_SettingsRoundTrip()
    {
        var minimap = new Minimap { Reveal = 0.5, EdgeColor = new Rgb(1, 2, 3) };
        var saved = minimap.SaveSettings()!;
        var loaded = new Minimap();
        loaded.LoadSettings(saved);
        Assert.Equal(0.5, loaded.Reveal);
        Assert.Equal(new Rgb(1, 2, 3), loaded.EdgeColor);
        Assert.Equal(Minimap.DefaultWalkableOpacity, loaded.WalkableOpacity);
        Assert.Null(new Minimap().SaveSettings());
    }

    [Fact]
    public void Camera_ClampsAndRoundsZoom()
    {
        Assert.Equal(Camera.MaxZoom, new Camera { ZoomLevel = 9 }.ZoomLevel);
        Assert.Equal(1.6, new Camera { ZoomLevel = 1.63 }.ZoomLevel);
        var camera = new Camera();
        camera.LoadSettings(new JsonObject { ["zoom"] = 1.8 });
        Assert.Equal(1.8, camera.ZoomLevel);
    }

    [Fact]
    public void LoadingScreen_HidesEveryBlockOnce()
    {
        const string ui = "begin Frame\r\n\tid = a;\r\n\tbegin Frame based on b\r\n\t\tid = b;\r\n\tend\r\nend";
        var once = LoadingScreen.Hide(ui)!;
        Assert.Equal(
            "begin Frame\r\n\tvisible = false;\r\n\tmouse_enabled = false;\r\n\tid = a;\r\n" +
            "\tbegin Frame based on b\r\n\t\tvisible = false;\r\n\t\tmouse_enabled = false;\r\n\t\tid = b;\r\n\tend\r\nend",
            once);
        Assert.Null(LoadingScreen.Hide(once));
    }

    [Fact]
    public void SwitchPatch_SavesOnlyTheSwitchesTurnedOff()
    {
        var light = new Light();
        Assert.Null(light.SaveSettings());
        light.Off.Add("player_light");
        var saved = light.SaveSettings()!;

        var loaded = new Light();
        loaded.LoadSettings(saved);
        Assert.Equal(["player_light"], loaded.Off);

        loaded.LoadSettings(new JsonObject { ["off"] = new JsonArray("no_such_switch") });
        Assert.Empty(loaded.Off);
    }

    [Fact]
    public void FolderSweep_SavesChangesFromTheDefaultList()
    {
        var mtx = new Mtx();
        mtx.Kept.Remove("metadata/effects/microtransactions/waypoint");
        mtx.Kept.Add("metadata/effects/microtransactions/pets");
        var saved = mtx.SaveSettings()!;

        var loaded = new Mtx();
        loaded.LoadSettings(saved);
        Assert.Contains("metadata/effects/microtransactions/pets", loaded.Kept);
        Assert.DoesNotContain("metadata/effects/microtransactions/waypoint", loaded.Kept);
        Assert.Contains("metadata/effects/microtransactions/town_portals", loaded.Kept);
    }

    [Fact]
    public void ColorMods_DefaultsMatchTheCatalog()
    {
        foreach (var game in Enum.GetValues<PoeGame>())
        {
            var catalog = ModLineCatalog.For(game).Select(l => l.Key).ToHashSet();
            if (catalog.Count == 0)
                continue; // catalog not generated yet
            foreach (var d in ModColorDefaults.For(game))
                Assert.True(catalog.Contains(d.Key), $"{game}: default {d.Key} has no catalog line");
            Assert.Equal(catalog.Count, ModLineCatalog.For(game).Count);
        }
    }

    [Fact]
    public void ColorMods_SettingsKeepOnlyChanges()
    {
        var mods = new ColorMods(PoeGame.PoE1);
        Assert.Null(mods.SaveSettings());

        mods.Groups[0].Color = new Rgb(1, 2, 3);
        var key = mods.Lines.First(l => !mods.Choices[l.Key].Enabled).Key;
        mods.Choices[key] = new ModLineChoice(true, ModColorDefaults.Good);
        var saved = mods.SaveSettings()!;

        var loaded = new ColorMods(PoeGame.PoE1);
        loaded.LoadSettings(saved);
        Assert.Equal(new Rgb(1, 2, 3), loaded.Groups[0].Color);
        Assert.Equal(new ModLineChoice(true, ModColorDefaults.Good), loaded.Choices[key]);
    }

    [Fact]
    public void ColorMods_ColorsOnTheFirstStatAndUndoesItsOwnMarkup()
    {
        const string csd =
            "description\r\n\t1 map_monsters_damage_+%\r\n\t1\r\n\t\t# \"{0}% increased Monster Damage\"\r\n\tlang \"German\"\r\n\t1\r\n\t\t# \"{0}% erhoeht\"\r\n" +
            "description\r\n\t1 map_monsters_life_+%\r\n\t1\r\n\t\t# \"<rgb(74,230,58)>{{{0}% more Monster Life}}\"\r\n";
        using var game = new SyntheticGame(new Dictionary<string, byte[]>
        {
            ["metadata/statdescriptions/map_stat_descriptions.csd"] = SyntheticGame.Utf16(csd),
        });
        var mods = new ColorMods(PoeGame.PoE1);
        mods.Choices["map_monsters_damage_+%"] = new ModLineChoice(true, ModColorDefaults.Dangerous);
        mods.Choices["map_monsters_life_+%"] = new ModLineChoice(false, ModColorDefaults.Annoying);

        game.Use(index => PatchRunner.Run(index, [mods], PoeGame.PoE1));

        var text = SyntheticGame.Text(game.ReadAll()["metadata/statdescriptions/map_stat_descriptions.csd"]);
        Assert.Contains("\"<rgb(209,46,46)>{{{0}% increased Monster Damage}}\"", text);
        Assert.Contains("\"<rgb(209,46,46)>{{{0}% erhoeht}}\"", text);
        Assert.Contains("\"{0}% more Monster Life\"", text);
        Assert.Equal(1, mods.ModsFound);
    }

    [Fact]
    public void Settings_SurviveASaveAndLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), "poeredux-tests", Guid.NewGuid().ToString("N"));
        AppSettings.FilePath = Path.Combine(dir, "settings.json");
        try
        {
            var patches = PatchCatalog.Create(PoeGame.PoE2);
            ((Camera)patches.First(p => p is Camera)).ZoomLevel = 1.6;
            var settings = new AppSettings { Game = PoeGame.PoE2 };
            settings.For(PoeGame.PoE2).Path = @"C:\Games\Content.ggpk";
            settings.For(PoeGame.PoE2).CaptureFrom(patches, ["camera", "minimap"]);
            settings.Save();

            var loaded = AppSettings.Load();
            Assert.Equal(PoeGame.PoE2, loaded.Game);
            Assert.Equal(@"C:\Games\Content.ggpk", loaded.For(PoeGame.PoE2).Path);
            Assert.Equal(["camera", "minimap"], loaded.For(PoeGame.PoE2).Selected!.Order());
            var fresh = PatchCatalog.Create(PoeGame.PoE2);
            loaded.For(PoeGame.PoE2).ApplyTo(fresh);
            Assert.Equal(1.6, ((Camera)fresh.First(p => p is Camera)).ZoomLevel);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PatchIds_AreUniquePerGame()
    {
        foreach (var game in Enum.GetValues<PoeGame>())
        {
            var ids = PatchCatalog.Create(game).Select(p => p.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }
}
