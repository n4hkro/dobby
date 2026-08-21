namespace Dobby.Configuration;

public sealed class AuthOptions
{
    public int[] LoginThrottleStepsSeconds { get; init; } = [0, 0, 10, 30, 60];

    public string PasswordKdfMode { get; init; } = "Pbkdf2Sha256";

    public int LoginThrottleFailureWindowSeconds { get; init; } = 900;

    public int LoginGlobalFailureThreshold { get; init; } = 10;

    public int LoginGlobalBackoffSeconds { get; init; } = 60;

    public int MaxConcurrentPasswordVerifications { get; init; } = 2;
}
