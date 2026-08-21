using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class PluginsOptionsTests
{
    [Fact]
    public void Pre_login_plugin_discovery_cannot_be_enabled_in_standalone_mode()
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

    [Fact]
    public void Package_resource_limits_have_safe_bounded_defaults()
    {
        var options = new PluginsOptions();

        options.MaximumCompressedPackageMegabytes.ShouldBe(64);
        options.MaximumManifestKilobytes.ShouldBe(256);
        options.MaximumEntryCount.ShouldBe(256);
        options.MaximumEntryUncompressedMegabytes.ShouldBe(64);
        options.MaximumAggregateUncompressedMegabytes.ShouldBe(256);
        options.MaximumExpansionRatio.ShouldBe(50);
    }

    [Theory]
    [InlineData(0, 256, 256, 64, 256, 50)]
    [InlineData(513, 256, 256, 64, 256, 50)]
    [InlineData(64, 1025, 256, 64, 256, 50)]
    [InlineData(64, 256, 2049, 64, 256, 50)]
    [InlineData(64, 256, 256, 513, 513, 50)]
    [InlineData(64, 256, 256, 64, 2049, 50)]
    [InlineData(64, 256, 256, 65, 64, 50)]
    [InlineData(64, 256, 256, 64, 256, 201)]
    public void Package_resource_limits_fail_closed_outside_hard_ceilings(
        int compressedMegabytes,
        int manifestKilobytes,
        int entryCount,
        int perEntryMegabytes,
        int aggregateMegabytes,
        int ratio)
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Plugins = new PluginsOptions
                {
                    MaximumCompressedPackageMegabytes = compressedMegabytes,
                    MaximumManifestKilobytes = manifestKilobytes,
                    MaximumEntryCount = entryCount,
                    MaximumEntryUncompressedMegabytes = perEntryMegabytes,
                    MaximumAggregateUncompressedMegabytes = aggregateMegabytes,
                    MaximumExpansionRatio = ratio
                }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(failure => failure.Contains("package limits", StringComparison.Ordinal));
    }
}
