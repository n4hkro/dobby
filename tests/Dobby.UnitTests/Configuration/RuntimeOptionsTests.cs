using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class RuntimeOptionsTests
{
    [Fact]
    public void Defaults_to_local_dobby_root_and_production_environment()
    {
        var options = new RuntimeOptions();

        options.DataRoot.ShouldContain("Dobby", Case.Insensitive);
        options.EnvironmentName.ShouldBe(RuntimeEnvironmentName.Production);
    }

    [Fact]
    public void Validator_rejects_network_data_roots()
    {
        var result = Validate(new DobbyOptions
        {
            Runtime = new RuntimeOptions { DataRoot = @"\\server\share\dobby" }
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("DataRoot", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("relative\\dobby")]
    [InlineData("..\\dobby")]
    public void Validator_rejects_relative_data_roots(string dataRoot)
    {
        var result = Validate(new DobbyOptions
        {
            Runtime = new RuntimeOptions { DataRoot = dataRoot }
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("DataRoot", StringComparison.Ordinal));
    }

    private static ValidateOptionsResult Validate(DobbyOptions options)
    {
        return new DobbyOptionsValidator().Validate(Options.DefaultName, options);
    }
}
