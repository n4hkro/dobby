namespace Dobby.Host.Settings;

public sealed record HostBuildMetadata(string BuildNumber, string BuildDate);

public interface IHostBuildMetadataProvider
{
    HostBuildMetadata Current { get; }
}
