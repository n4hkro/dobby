using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Dobby.Host.Login;

internal sealed class LoginStartupTimingTrace
{
    private readonly long startedTimestamp;
    private long bootstrapCompletedTimestamp;
    private long snapshotLoadedTimestamp;
    private long themeAppliedTimestamp;
    private long loginWindowConstructedTimestamp;
    private long loginContentRenderedTimestamp;
    private long loginReadyTimestamp;

    private LoginStartupTimingTrace(long startedTimestamp)
    {
        this.startedTimestamp = startedTimestamp;
    }

    public static LoginStartupTimingTrace Start()
    {
        return new LoginStartupTimingTrace(Stopwatch.GetTimestamp());
    }

    public void MarkBootstrapCompleted()
    {
        bootstrapCompletedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkSnapshotLoaded()
    {
        snapshotLoadedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkThemeApplied()
    {
        themeAppliedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkLoginWindowConstructed()
    {
        loginWindowConstructedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkLoginContentRendered()
    {
        loginContentRenderedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkLoginReady()
    {
        loginReadyTimestamp = Stopwatch.GetTimestamp();
    }

    public Task FlushAfterFirstPaintAsync()
    {
        var line = BuildLogLine();
        return Task.Run(() => WriteLogLine(line));
    }

    private string BuildLogLine()
    {
        var firstPaintTimestamp = loginContentRenderedTimestamp == 0
            ? Stopwatch.GetTimestamp()
            : loginContentRenderedTimestamp;
        var snapshotStartedTimestamp = bootstrapCompletedTimestamp == 0
            ? startedTimestamp
            : bootstrapCompletedTimestamp;
        return string.Join(
            " ",
            $"timestamp={DateTimeOffset.UtcNow:O}",
            $"total_ms={ElapsedMilliseconds(startedTimestamp, firstPaintTimestamp)}",
            $"bootstrap_ms={ElapsedMilliseconds(startedTimestamp, bootstrapCompletedTimestamp)}",
            $"snapshot_ms={ElapsedMilliseconds(snapshotStartedTimestamp, snapshotLoadedTimestamp)}",
            $"theme_ms={ElapsedMilliseconds(snapshotLoadedTimestamp, themeAppliedTimestamp)}",
            $"window_ms={ElapsedMilliseconds(themeAppliedTimestamp, loginWindowConstructedTimestamp)}",
            $"first_paint_ms={ElapsedMilliseconds(loginWindowConstructedTimestamp, firstPaintTimestamp)}",
            $"ready_ms={ElapsedMilliseconds(startedTimestamp, loginReadyTimestamp)}");
    }

    private static string ElapsedMilliseconds(long start, long end)
    {
        if (start == 0 || end == 0 || end < start)
        {
            return "n/a";
        }

        return Stopwatch.GetElapsedTime(start, end).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
    }

    private static void WriteLogLine(string line)
    {
        try
        {
            var path = LogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    private static string LogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Dobby", "logs", "login-startup-timing.log");
    }
}
