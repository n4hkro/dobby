namespace Dobby.Configuration;

public sealed class StorageOptions
{
    public StorageProviderMode ProviderMode { get; init; } = StorageProviderMode.SQLite3MultipleCiphers;

    public bool RequireDataAtRestProtection { get; init; } = true;

    public int BusyTimeoutMilliseconds { get; init; } = 5000;
}
