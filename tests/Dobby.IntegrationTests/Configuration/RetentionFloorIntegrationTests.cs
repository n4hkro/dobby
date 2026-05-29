using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.IntegrationTests.Configuration;

public sealed class RetentionFloorIntegrationTests
{
    [Fact]
    public void AC_052_retention_floors_and_finite_diagnostics_cap_are_enforced_together()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Audit = new AuditOptions { RetentionDays = 364 },
                Diagnostics = new DiagnosticsOptions
                {
                    TechnicalLogRetentionDays = 29,
                    TechnicalLogTotalSizeCapMegabytes = 0
                }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("Audit", StringComparison.Ordinal));
        result.Failures.ShouldContain(x => x.Contains("Diagnostics", StringComparison.Ordinal));
    }
}
