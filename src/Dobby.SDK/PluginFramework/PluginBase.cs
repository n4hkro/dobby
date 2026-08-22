using Dobby.Contracts.PluginFramework;
using Dobby.Core;

namespace Dobby.SDK.PluginFramework;

public abstract class PluginBase : IPluginModule
{
    public abstract void RegisterServices(IPluginServiceCollection services);

    public virtual void OnActivated(IPluginActivationContext context)
    {
    }
}

public abstract class PluginBaseV2 : PluginBase, IPluginModuleV2
{
    public sealed override void OnActivated(IPluginActivationContext context)
    {
        // V2 activation is always dispatched through ActivateAsync by the host.
    }

    public virtual ValueTask<Result> InitializeAsync(
        IPluginActivationContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());

    public virtual ValueTask<Result> ActivateAsync(
        IPluginActivationContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());

    public virtual ValueTask<Result> DeactivateAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());

    public virtual ValueTask<Result> UnloadAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());
}
