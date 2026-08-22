using Dobby.Core;

namespace Dobby.PluginFramework.Packaging;

public sealed record PluginInstallCommitContext(
    string ActorId,
    CorrelationId CorrelationId);

public interface IPluginInstallStateCommitter
{
    Result Commit(
        PluginPackageDescriptor package,
        InstalledPluginState installedState,
        InstalledPluginMetadata metadata,
        PluginInstallCommitContext context);
}
