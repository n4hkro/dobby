using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class AuditOptionsTests
{
    [Fact]
    public void Audit_retention_cannot_go_below_365_days()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Audit = new AuditOptions { RetentionDays = 364 }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("RetentionDays", StringComparison.Ordinal));
    }
}
