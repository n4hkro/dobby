using Dobby.Host;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class ProcessExitEscalatorTests
{
    [Fact]
    public void BeginExit_schedules_graceful_exit_then_hard_kill_once()
    {
        var scheduled = new List<(TimeSpan Delay, Action Callback)>();
        var exitCodes = new List<int>();
        var killCount = 0;
        var escalator = new ProcessExitEscalator(
            TimeSpan.FromMilliseconds(750),
            TimeSpan.FromMilliseconds(2_000),
            (delay, callback) => scheduled.Add((delay, callback)),
            exitCodes.Add,
            () => killCount++);

        escalator.BeginExit();
        escalator.BeginExit();

        scheduled.Count.ShouldBe(2);
        scheduled[0].Delay.ShouldBe(TimeSpan.FromMilliseconds(750));
        scheduled[1].Delay.ShouldBe(TimeSpan.FromMilliseconds(2_000));

        scheduled[0].Callback();
        scheduled[1].Callback();

        exitCodes.ShouldBe([0]);
        killCount.ShouldBe(1);
    }

    [Fact]
    public void Default_escalator_uses_immediate_exit_and_fast_hard_kill_fallback()
    {
        var escalator = ProcessExitEscalator.CreateDefault();

        escalator.GracefulExitDelay.ShouldBe(TimeSpan.Zero);
        escalator.HardKillDelay.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void App_and_shell_start_exit_escalation_for_real_application_close_paths()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        app.ShouldContain("ShutdownHostProcess();");
        app.ShouldContain("ProcessExitEscalation.Begin();");
        mainWindow.ShouldContain("BeginApplicationExit();");
        mainWindow.ShouldContain("ProcessExitEscalation.Begin();");
        mainWindow.IndexOf("ProcessExitEscalation.Begin();", StringComparison.Ordinal)
            .ShouldBeLessThan(mainWindow.IndexOf("Close();", StringComparison.Ordinal));
    }

    private static string ReadSource(params string[] relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot().FullName, Path.Combine(relativePath)));
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        current.ShouldNotBeNull();
        return current;
    }
}
