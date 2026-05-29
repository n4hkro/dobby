namespace Dobby.Security.Authentication;

public sealed record LoginThrottleState(bool IsThrottled, int RetryAfterSeconds);

public sealed class LoginThrottleService
{
    private readonly int[] stepsSeconds;
    private readonly TimeProvider timeProvider;
    private int failedAttempts;
    private DateTimeOffset throttledUntil;

    public LoginThrottleService(IReadOnlyList<int> stepsSeconds, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(stepsSeconds);
        this.stepsSeconds = stepsSeconds.Count == 0 ? [0, 0, 10, 30, 60] : stepsSeconds.ToArray();
        this.timeProvider = timeProvider;
    }

    public LoginThrottleState Check()
    {
        var remaining = RemainingSeconds();
        return new LoginThrottleState(remaining > 0, remaining);
    }

    public LoginThrottleState RecordFailure(string? username)
    {
        failedAttempts++;
        var delaySeconds = stepsSeconds[Math.Min(failedAttempts - 1, stepsSeconds.Length - 1)];
        throttledUntil = timeProvider.GetUtcNow().AddSeconds(delaySeconds);
        return Check();
    }

    public void RecordSuccess()
    {
        failedAttempts = 0;
        throttledUntil = DateTimeOffset.MinValue;
    }

    private int RemainingSeconds()
    {
        var remaining = throttledUntil - timeProvider.GetUtcNow();
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }
}
