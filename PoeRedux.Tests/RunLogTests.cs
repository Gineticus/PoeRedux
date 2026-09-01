using PoeRedux.Services;

namespace PoeRedux.Tests;

/// <summary>
/// Runs against a temp directory via RunLog.DirectoryOverride, never the real
/// %LOCALAPPDATA%\PoeRedux\Logs\: a retention test creating and pruning 25 files must not risk
/// touching real logs from this machine's actual Apply/Restore runs.
/// </summary>
public class RunLogTests : IDisposable
{
    private readonly string _tempDir;

    public RunLogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PoeReduxRunLogTests_" + Guid.NewGuid());
        RunLog.DirectoryOverride = _tempDir;
    }

    public void Dispose()
    {
        RunLog.DirectoryOverride = null;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RoundTrip_WritesExactLinesToARealFile()
    {
        RunLog.Begin("Apply", PoeGame.PoE1);
        RunLog.Line("patch: Camera");
        RunLog.Line("  backup metadata/characters/character.ot: CDN vanilla");
        RunLog.End();

        var files = Directory.GetFiles(_tempDir, "*.log");
        Assert.Single(files);

        var content = File.ReadAllText(files[0]);
        Assert.Contains("Apply started", content);
        Assert.Contains("game: PoE1", content);
        Assert.Contains("patch: Camera", content);
        Assert.Contains("backup metadata/characters/character.ot: CDN vanilla", content);
        Assert.Contains("ended", content);
    }

    [Fact]
    public void FileName_NamesTheKindAndGame()
    {
        RunLog.Begin("Restore", PoeGame.PoE2);
        RunLog.End();

        var files = Directory.GetFiles(_tempDir, "*.log");
        Assert.Single(files);
        Assert.Contains("Restore", Path.GetFileName(files[0]));
        Assert.Contains("PoE2", Path.GetFileName(files[0]));
    }

    [Fact]
    public void CurrentPath_ExposesTheActiveLogWhileOpen()
    {
        RunLog.Begin("Apply", PoeGame.PoE1);
        Assert.NotNull(RunLog.CurrentPath);
        Assert.True(File.Exists(RunLog.CurrentPath));
        RunLog.End();
        Assert.Null(RunLog.CurrentPath);
    }

    [Fact]
    public void Exception_LogsTheFullStackTraceNotJustTheMessage()
    {
        RunLog.Begin("Apply", PoeGame.PoE1);
        try
        {
            ThrowsWithAStack();
        }
        catch (Exception ex)
        {
            RunLog.Exception(ex);
        }
        RunLog.End();

        var content = File.ReadAllText(Directory.GetFiles(_tempDir, "*.log")[0]);
        Assert.Contains("EXCEPTION:", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("deliberate test failure", content);
        Assert.Contains("at PoeRedux.Tests.RunLogTests", content); // a real stack frame, not just ex.Message
    }

    private static void ThrowsWithAStack() =>
        throw new InvalidOperationException("deliberate test failure");

    [Fact]
    public void Retention_KeepsOnlyTheNewest20_OldestDeletedFirst()
    {
        Directory.CreateDirectory(_tempDir);
        var names = new List<string>();
        for (var i = 0; i < 25; i++)
        {
            // Timestamp-first names so ascending i sorts as ascending time, oldest to newest.
            var name = $"2020-01-{i + 1:00}_00-00-00_Apply_PoE1.log";
            File.WriteAllText(Path.Combine(_tempDir, name), "x");
            names.Add(name);
        }

        // Begin()/End() itself creates one more, real, 2026-dated file, which sorts newest of
        // all 26 and always survives: 20 keep-slots leaves room for 19 of the 25 fake ones.
        RunLog.Begin("Apply", PoeGame.PoE1);
        RunLog.End();

        var remaining = Directory.GetFiles(_tempDir, "*.log")
            .Select(Path.GetFileName)
            .ToHashSet();

        Assert.Equal(20, remaining.Count);
        for (var i = 0; i < 6; i++)
            Assert.DoesNotContain(names[i], remaining);
        for (var i = 6; i < 25; i++)
            Assert.Contains(names[i], remaining);
    }
}
