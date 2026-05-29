using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class DiagnosticsOptionsTests
{
    [Fact]
    public void Diagnostics_retention_and_size_cap_floors_are_fail_closed()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Diagnostics = new DiagnosticsOptions
                {
                    TechnicalLogRetentionDays = 29,
                    TechnicalLogTotalSizeCapMegabytes = 0
                }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("TechnicalLogRetentionDays", StringComparison.Ordinal));
        result.Failures.ShouldContain(x => x.Contains("TechnicalLogTotalSizeCapMegabytes", StringComparison.Ordinal));
    }
}
