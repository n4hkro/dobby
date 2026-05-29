namespace Dobby.Configuration;

public sealed class DobbyOptions
{
    public RuntimeOptions Runtime { get; init; } = new();

    public StorageOptions Storage { get; init; } = new();

    public AuthOptions Auth { get; init; } = new();

    public SessionOptions Session { get; init; } = new();

    public DiagnosticsOptions Diagnostics { get; init; } = new();

    public AuditOptions Audit { get; init; } = new();

    public LocalizationOptions Localization { get; init; } = new();

    public PluginsOptions Plugins { get; init; } = new();

    public LicensingOptions Licensing { get; init; } = new();

    public BranchSharedOptions BranchShared { get; init; } = new();
}
