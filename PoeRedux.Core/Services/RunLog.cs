using System.Globalization;
using System.Text;

namespace PoeRedux.Services;

/// <summary>
/// A plain-text record of one apply or restore, in %LOCALAPPDATA%\PoeRedux\Logs. Attach it to a bug report.
/// The newest <see cref="Keep"/> logs are kept.
/// </summary>
public sealed class RunLog : IDisposable
{
    private const int Keep = 20;

    public static string LogDirectory { get; set; } = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "PoeRedux", "Logs");

    private readonly StreamWriter? _writer;

    private RunLog(string path, StreamWriter? writer)
    {
        FilePath = path;
        _writer = writer;
    }

    public string FilePath { get; }

    /// <summary>Starts a log. Logging never stops an apply: if the file cannot be written, lines are dropped.</summary>
    public static RunLog Start(string action, string game)
    {
        var name = $"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture)} {action} {game}.log";
        var path = Path.Combine(LogDirectory, name);
        StreamWriter? writer = null;
        try
        {
            Directory.CreateDirectory(LogDirectory);
            writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
            foreach (var old in Directory.GetFiles(LogDirectory, "*.log").Order(StringComparer.Ordinal).SkipLast(Keep))
                File.Delete(old);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        var log = new RunLog(path, writer);
        log.Line($"PoE Redux {typeof(RunLog).Assembly.GetName().Version}, {action}, {game}");
        return log;
    }

    public void Line(string text)
    {
        try { _writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}"); }
        catch (IOException) { }
    }

    public void Dispose() => _writer?.Dispose();
}
