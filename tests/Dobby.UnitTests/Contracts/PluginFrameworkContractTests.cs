using Dobby.Contracts.PluginFramework;
using Dobby.SDK.PluginFramework;
using Shouldly;

namespace Dobby.UnitTests.Contracts;

public sealed class PluginFrameworkContractTests
{
    [Fact]
    public void Plugin_base_defaults_do_not_register_extra_lifecycle_hooks()
    {
        var plugin = new MinimalPlugin();
        var services = new RecordingPluginServiceCollection();

        plugin.RegisterServices(services);
        plugin.OnActivated(new PluginActivationContext("com.dobby.sample", "1.0.0", Dobby.Core.CorrelationId.New()));

        services.Registrations.ShouldBeEmpty();
    }

    [Fact]
    public void PluginBase_OnActivated_default_is_no_op()
    {
        var plugin = new MinimalPlugin();

        Should.NotThrow(() => plugin.OnActivated(null!));
    }

    private sealed class MinimalPlugin : PluginBase
    {
        public override void RegisterServices(IPluginServiceCollection services)
        {
        }
    }

    private sealed class RecordingPluginServiceCollection : IPluginServiceCollection
    {
        public List<string> Registrations { get; } = [];

        public IPluginServiceCollection AddInternal<TService, TImpl>(PluginLifetime lifetime)
            where TService : class
            where TImpl : class, TService
        {
            Registrations.Add($"internal:{typeof(TService).Name}:{lifetime}");
            return this;
        }

        public IPluginServiceCollection AddExtensionPoint<TContract, TImpl>()
            where TContract : class
            where TImpl : class, TContract
        {
            Registrations.Add($"extension:{typeof(TContract).Name}");
            return this;
        }
    }
}
