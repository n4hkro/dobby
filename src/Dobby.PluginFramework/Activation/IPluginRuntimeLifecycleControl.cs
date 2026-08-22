using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public interface IPluginRuntimeLifecycleControl
{
    ValueTask<Result> DeactivateAndUnloadAsync(
        string pluginId,
        CancellationToken cancellationToken);
}
