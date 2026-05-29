namespace Dobby.Configuration;

public sealed class DiagnosticsOptions
{
    public int TechnicalLogRetentionDays { get; init; } = 30;

    public int TechnicalLogTotalSizeCapMegabytes { get; init; } = 256;
}
