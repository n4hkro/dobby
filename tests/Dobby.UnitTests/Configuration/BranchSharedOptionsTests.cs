using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class BranchSharedOptionsTests
{
    [Fact]
    public void Branch_shared_runtime_stays_disabled_in_p1()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                BranchShared = new BranchSharedOptions { RuntimeEnabled = true }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("RuntimeEnabled", StringComparison.Ordinal));
    }
}
