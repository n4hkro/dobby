namespace Dobby.Diagnostics;

public sealed record StartupMilestone(
    string Name,
    DateTimeOffset OccurredAt,
    string CorrelationId);
