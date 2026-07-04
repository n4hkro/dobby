using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Dobby.Host.Login;

internal sealed class PostLoginTimingTrace
{
    private readonly long startedTimestamp;
    private long loginSubmitCompletedTimestamp;
    private long runtimeReadyTimestamp;
    private long shellCompositionStartedTimestamp;
    private long shellWindowConstructedTimestamp;
    private long shellRuntimeAttachedTimestamp;
    private long shellFirstPaintTimestamp;

    private PostLoginTimingTrace(long startedTimestamp)
    {
        this.startedTimestamp = startedTimestamp;
    }

    public static PostLoginTimingTrace Start()
    {
        return new PostLoginTimingTrace(Stopwatch.GetTimestamp());
    }

    public void MarkLoginSubmitCompleted()
    {
        loginSubmitCompletedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkRuntimeReady()
    {
        runtimeReadyTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkShellCompositionStarted()
    {
        shellCompositionStartedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkShellWindowConstructed()
    {
        shellWindowConstructedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkShellRuntimeAttached()
    {
        shellRuntimeAttachedTimestamp = Stopwatch.GetTimestamp();
    }

    public void MarkShellFirstPaint()
    {
        shellFirstPaintTimestamp = Stopwatch.GetTimestamp();
    }

    public Task FlushAfterShellFirstPaintAsync()
    {
        var line = BuildLogLine();
        return Task.Run(() => WriteLogLine(line));
    }

    private string BuildLogLine()
    {
        var firstPaint = shellFirstPaintTimestamp == 0
            ? Stopwatch.GetTimestamp()
            : shellFirstPaintTimestamp;
        return string.Join(
            " ",
            $"timestamp={DateTimeOffset.UtcNow:O}",
            $"total_ms={ElapsedMilliseconds(startedTimestamp, firstPaint)}",
            $"submit_ms={ElapsedMilliseconds(startedTimestamp, loginSubmitCompletedTimestamp)}",
            $"runtime_wait_ms={ElapsedMilliseconds(loginSubmitCompletedTimestamp, runtimeReadyTimestamp)}",
            $"shell_start_wait_ms={ElapsedMilliseconds(runtimeReadyTimestamp, shellCompositionStartedTimestamp)}",
            $"shell_window_ms={ElapsedMilliseconds(shellCompositionStartedTimestamp, shellWindowConstructedTimestamp)}",
            $"shell_attach_ms={ElapsedMilliseconds(shellWindowConstructedTimestamp, shellRuntimeAttachedTimestamp)}",
            $"shell_first_paint_ms={ElapsedMilliseconds(shellRuntimeAttachedTimestamp, firstPaint)}");
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
        return Path.Combine(localAppData, "Dobby", "logs", "post-login-timing.log");
    }
}
