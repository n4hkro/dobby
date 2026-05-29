using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class PluginsOptionsTests
{
    [Fact]
    public void Pre_login_plugin_discovery_cannot_be_enabled_in_p1()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Plugins = new PluginsOptions { AllowPreLoginDiscovery = true }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("AllowPreLoginDiscovery", StringComparison.Ordinal));
    }
}
