using LibBundle3.Nodes;
using PoeRedux.Helpers;
using PoeRedux.Models;
using PoeRedux.Patches;
using PoeRedux.Services;
using System.IO.Compression;
using Xunit;

namespace PoeRedux.Tests;

public class BackupFlowTests
{
    private const PoeGame Game = PoeGame.PoE1;

    private static Dictionary<string, byte[]> Files() => new()
    {
        ["metadata/environmentsettings/a.env"] = SyntheticGame.Utf16("{\r\n\t\"fog\": { \"d\": 1 },\r\n\t\"rain_intensity\": 0.8\r\n}"),
        ["metadata/environmentsettings/b.env"] = SyntheticGame.Utf16("{\r\n\t\"fog\": { \"d\": 2 }\r\n}"),
        ["metadata/environmentsettings/c.env"] = SyntheticGame.Utf16("{\r\n\t\"area\": {}\r\n}"),
        ["metadata/ui/loadingstate/loadingscreen.ui"] = SyntheticGame.Utf16("\r\n<DEFINITIONS>\r\n\r\nbegin Frame\r\n\tid = x;\r\nend\r\n"),
        ["data/other.txt"] = SyntheticGame.Utf16("untouched"),
    };

    private static PatchResult Apply(SyntheticGame game, params IPatch[] patches) =>
        game.Use(index => PatchRunner.Run(index, patches, Game));

    private static PatchResult Restore(SyntheticGame game) =>
        game.Use(index => PatchRunner.Restore(index, Game), parsePaths: false);

    private static EnvironmentLayers FogOnly()
    {
        var patch = new EnvironmentLayers();
        foreach (var s in patch.Switches.Where(s => s.Id != "fog"))
            patch.Off.Add(s.Id);
        return patch;
    }

    [Fact]
    public void ApplyThenRestore_GivesBackEveryOriginalByte()
    {
        var original = Files();
        using var game = new SyntheticGame(original);

        var applied = Apply(game, new EnvironmentLayers(), new LoadingScreen());
        Assert.Equal(4, applied.FilesWritten);
        Assert.Equal(4, applied.FilesBackedUp);
        Assert.Contains("\"xog\"", SyntheticGame.Text(game.ReadAll()["metadata/environmentsettings/a.env"]));

        var restored = Restore(game);
        Assert.Equal(4, restored.FilesRestored);
        Assert.Equal(original, game.ReadAll());
        Assert.False(BackupStore.HasBackup(Game));
    }

    [Fact]
    public void ApplyingAgain_StartsFromTheOriginal()
    {
        using var game = new SyntheticGame(Files());
        Apply(game, new LoadingScreen());
        var once = game.ReadAll();

        var second = Apply(game, new LoadingScreen());

        Assert.Equal(0, second.FilesWritten);
        Assert.Equal(once, game.ReadAll());
        var text = SyntheticGame.Text(once["metadata/ui/loadingstate/loadingscreen.ui"]);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "visible = false;"));
    }

    [Fact]
    public void UntickedPatch_GetsItsOriginalsBack()
    {
        var original = Files();
        using var game = new SyntheticGame(original);
        Apply(game, new EnvironmentLayers(), new LoadingScreen());

        var result = Apply(game, new LoadingScreen());

        Assert.Equal(3, result.FilesRestored);
        var files = game.ReadAll();
        foreach (var env in original.Keys.Where(k => k.EndsWith(".env")))
            Assert.Equal(original[env], files[env]);
        Assert.NotEqual(original["metadata/ui/loadingstate/loadingscreen.ui"], files["metadata/ui/loadingstate/loadingscreen.ui"]);
    }

    [Fact]
    public void ChangedSetting_IsAppliedToTheOriginal()
    {
        var original = Files();
        using var game = new SyntheticGame(original);
        Apply(game, new EnvironmentLayers());

        Apply(game, FogOnly());

        var a = SyntheticGame.Text(game.ReadAll()["metadata/environmentsettings/a.env"]);
        Assert.Contains("\"xog\"", a);
        Assert.Contains("0.8", a); // rain is no longer zeroed
        Assert.Equal(original["metadata/environmentsettings/c.env"], game.ReadAll()["metadata/environmentsettings/c.env"]);
    }

    [Fact]
    public void Restore_LeavesFilesTheGameReplaced()
    {
        var original = Files();
        using var game = new SyntheticGame(original);
        Apply(game, new EnvironmentLayers());
        var update = SyntheticGame.Utf16("{\r\n\t\"fog\": { \"d\": 9 }\r\n}");
        game.Update(new Dictionary<string, byte[]> { ["metadata/environmentsettings/b.env"] = update });

        var restored = Restore(game);

        Assert.Equal(1, restored.FilesReplacedByGame);
        var files = game.ReadAll();
        Assert.Equal(update, files["metadata/environmentsettings/b.env"]);
        Assert.Equal(original["metadata/environmentsettings/a.env"], files["metadata/environmentsettings/a.env"]);
    }

    [Fact]
    public void ApplyAfterGameUpdate_BacksUpTheNewVersion()
    {
        var original = Files();
        using var game = new SyntheticGame(original);
        Apply(game, new EnvironmentLayers());
        var update = SyntheticGame.Utf16("{\r\n\t\"fog\": { \"d\": 9 }\r\n}");
        game.Update(new Dictionary<string, byte[]> { ["metadata/environmentsettings/b.env"] = update });

        var applied = Apply(game, new EnvironmentLayers());

        Assert.Equal(1, applied.FilesReplacedByGame);
        Assert.Equal(1, applied.FilesBackedUp);
        Assert.Contains("\"d\": 9", SyntheticGame.Text(game.ReadAll()["metadata/environmentsettings/b.env"]));

        Restore(game);
        var files = game.ReadAll();
        Assert.Equal(update, files["metadata/environmentsettings/b.env"]);
        Assert.Equal(original["metadata/environmentsettings/a.env"], files["metadata/environmentsettings/a.env"]);
    }

    [Fact]
    public void BackupFromAnOlderVersion_IsRestoredAsBefore()
    {
        var original = Files();
        using var game = new SyntheticGame(original);
        // What 1.1.18 to 1.1.20 left: patched files and a zip of originals, without a backup list.
        var patched = SyntheticGame.Utf16("{\r\n\t\"xog\": { \"d\": 1 },\r\n\t\"rain_intensity\": 0.8\r\n}");
        game.Update(new Dictionary<string, byte[]> { ["metadata/environmentsettings/a.env"] = patched });
        Directory.CreateDirectory(game.BackupDirectory);
        using (var zip = ZipFile.Open(Path.Combine(game.BackupDirectory, "poe1.zip"), ZipArchiveMode.Create))
        using (var stream = zip.CreateEntry("metadata/environmentsettings/a.env").Open())
            stream.Write(original["metadata/environmentsettings/a.env"]);

        var restored = Restore(game);

        Assert.Equal(1, restored.FilesRestored);
        Assert.Equal(original, game.ReadAll());
    }

    [Fact]
    public void PatchWithoutTargets_IsReported()
    {
        using var game = new SyntheticGame(Files());
        var camera = new Camera();

        var result = Apply(game, camera);

        Assert.Equal([camera], result.PatchesWithoutFiles);
        Assert.Equal(0, result.FilesWritten);
    }
}
