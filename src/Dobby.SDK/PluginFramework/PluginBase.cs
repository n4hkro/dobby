using Dobby.Contracts.PluginFramework;

namespace Dobby.SDK.PluginFramework;

public abstract class PluginBase : IPluginModule
{
    public abstract void RegisterServices(IPluginServiceCollection services);

    public virtual void OnActivated(IPluginActivationContext context)
    {
    }
}
