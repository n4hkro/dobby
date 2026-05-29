namespace Dobby.Host.Startup;

public sealed record StartupPlan(
    IReadOnlyList<string> PreLoginSteps,
    IReadOnlyList<string> PostLoginSteps);
