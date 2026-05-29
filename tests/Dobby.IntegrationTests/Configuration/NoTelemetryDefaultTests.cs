using Dobby.Configuration;
using Shouldly;

namespace Dobby.IntegrationTests.Configuration;

public sealed class NoTelemetryDefaultTests
{
    [Fact]
    public void Configuration_schema_exposes_no_telemetry_option_to_enable_uploads()
    {
        typeof(DobbyOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain("Telemetry");
    }
}
