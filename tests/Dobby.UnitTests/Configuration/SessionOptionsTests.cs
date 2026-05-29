using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class SessionOptionsTests
{
    [Fact]
    public void Session_timeout_ranges_are_fail_closed()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Session = new SessionOptions
                {
                    AbsoluteTimeoutMinutes = 10,
                    IdleTimeoutMinutes = 4
                }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("AbsoluteTimeoutMinutes", StringComparison.Ordinal));
        result.Failures.ShouldContain(x => x.Contains("IdleTimeoutMinutes", StringComparison.Ordinal));
    }
}
