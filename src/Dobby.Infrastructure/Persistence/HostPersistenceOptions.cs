using System.IO;

namespace Dobby.Infrastructure.Persistence;

public sealed record HostPersistenceOptions(
    string DataRoot,
    string ProviderMode,
    bool RequireDataAtRestProtection,
    int BusyTimeoutMilliseconds)
{
    public string DatabasePath => Path.Combine(
        Environment.ExpandEnvironmentVariables(DataRoot),
        "data",
        "host.db");
}
