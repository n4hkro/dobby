using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class LicensingOptionsTests
{
    [Fact]
    public void License_import_permission_floor_cannot_be_disabled()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Licensing = new LicensingOptions { ImportRequiresPermission = false }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("ImportRequiresPermission", StringComparison.Ordinal));
    }
}
