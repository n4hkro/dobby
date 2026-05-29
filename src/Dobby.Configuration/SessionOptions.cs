namespace Dobby.Configuration;

public sealed class SessionOptions
{
    public int AbsoluteTimeoutMinutes { get; init; } = 480;

    public int IdleTimeoutMinutes { get; init; } = 30;

    public bool AutoLockEnabledByDefault { get; init; }
}
