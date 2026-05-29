namespace Dobby.Host.Startup;

public sealed record HostBootstrapOptions(
    bool ShowMainWindow = true,
    string? PackagedAppSettingsPath = null,
    string? RuntimeConfigDirectory = null,
    string? EnvironmentName = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null,
    Func<bool>? ProtectedMaterialPrerequisiteAvailable = null,
    Func<Dobby.Configuration.DobbyOptions, bool>? PreLoginPersistenceOpen = null,
    Func<ISingleInstanceCoordinator>? SingleInstanceCoordinatorFactory = null,
    PreLoginInstrumentation? PreLoginInstrumentation = null);
