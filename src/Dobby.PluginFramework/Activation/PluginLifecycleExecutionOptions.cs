namespace Dobby.PluginFramework.Activation;

public sealed record PluginLifecycleExecutionOptions(
    TimeSpan InitializationTimeout,
    TimeSpan ActivationTimeout,
    TimeSpan DeactivationTimeout,
    TimeSpan UnloadTimeout)
{
    public static PluginLifecycleExecutionOptions Default { get; } = new(
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15));

    public void Validate()
    {
        ValidateTimeout(InitializationTimeout, nameof(InitializationTimeout));
        ValidateTimeout(ActivationTimeout, nameof(ActivationTimeout));
        ValidateTimeout(DeactivationTimeout, nameof(DeactivationTimeout));
        ValidateTimeout(UnloadTimeout, nameof(UnloadTimeout));
    }

    private static void ValidateTimeout(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.FromMilliseconds(100) || value > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
