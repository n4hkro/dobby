using Dobby.Contracts.Licensing;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicensingTests
{
    [Theory]
    [InlineData(LicenseArtifactStatus.Active, PluginRuntimeMode.FullAccess)]
    [InlineData(LicenseArtifactStatus.Pending, PluginRuntimeMode.Denied)]
    [InlineData(LicenseArtifactStatus.ReadOnly, PluginRuntimeMode.ReadOnlyExpired)]
    [InlineData(LicenseArtifactStatus.Denied, PluginRuntimeMode.Denied)]
    public void License_artifact_status_maps_to_non_disclosing_runtime_mode(
        LicenseArtifactStatus status,
        PluginRuntimeMode expected)
    {
        LicenseRuntimeModeMapper.Map(status).ShouldBe(expected);
    }
}
