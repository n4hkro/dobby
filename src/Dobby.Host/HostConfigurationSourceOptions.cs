namespace Dobby.Host;

public sealed record HostConfigurationSourceOptions(
    string PackagedAppSettingsPath,
    string RuntimeConfigDirectory,
    string EnvironmentName,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null);
