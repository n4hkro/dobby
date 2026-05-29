namespace Dobby.Configuration;

public sealed class AuditOptions
{
    public int RetentionDays { get; init; } = 365;
}
