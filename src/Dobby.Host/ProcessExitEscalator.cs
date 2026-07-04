using System.Diagnostics;

namespace Dobby.Host;

public sealed class ProcessExitEscalator
{
    private const int SuccessExitCode = 0;
    private readonly TimeSpan gracefulExitDelay;
    private readonly TimeSpan hardKillDelay;
    private readonly Action<TimeSpan, Action> schedule;
    private readonly Action<int> exitProcess;
    private readonly Action killProcess;
    private int hasBegun;

    public ProcessExitEscalator(
        TimeSpan gracefulExitDelay,
        TimeSpan hardKillDelay,
        Action<TimeSpan, Action> schedule,
        Action<int> exitProcess,
        Action killProcess)
    {
        if (gracefulExitDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(gracefulExitDelay));
        }

        if (hardKillDelay < gracefulExitDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(hardKillDelay));
        }

        this.gracefulExitDelay = gracefulExitDelay;
        this.hardKillDelay = hardKillDelay;
        this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        this.exitProcess = exitProcess ?? throw new ArgumentNullException(nameof(exitProcess));
        this.killProcess = killProcess ?? throw new ArgumentNullException(nameof(killProcess));
    }

    public TimeSpan GracefulExitDelay => gracefulExitDelay;

    public TimeSpan HardKillDelay => hardKillDelay;

    public static ProcessExitEscalator CreateDefault()
    {
        return new ProcessExitEscalator(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(250),
            ScheduleOnThreadPool,
            Environment.Exit,
            () => Process.GetCurrentProcess().Kill(entireProcessTree: true));
    }

    public void BeginExit()
    {
        if (Interlocked.Exchange(ref hasBegun, 1) != 0)
        {
            return;
        }

        schedule(gracefulExitDelay, () => exitProcess(SuccessExitCode));
        schedule(hardKillDelay, killProcess);
    }

    private static void ScheduleOnThreadPool(TimeSpan delay, Action callback)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay).ConfigureAwait(false);
            callback();
        });
    }
}

public static class ProcessExitEscalation
{
    private static readonly ProcessExitEscalator Default = ProcessExitEscalator.CreateDefault();

    public static void Begin()
    {
        Default.BeginExit();
    }
}
