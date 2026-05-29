using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class ConfigurationTests
{
    [Fact]
    public void Runtime_environment_and_storage_provider_are_explicit_enum_contracts()
    {
        typeof(RuntimeOptions).GetProperty(nameof(RuntimeOptions.EnvironmentName))!
            .PropertyType.ShouldBe(typeof(RuntimeEnvironmentName));
        typeof(StorageOptions).GetProperty(nameof(StorageOptions.ProviderMode))!
            .PropertyType.ShouldBe(typeof(StorageProviderMode));
    }

    [Fact]
    public void Security_floors_cannot_be_weakened_by_config()
    {
        var validator = new DobbyOptionsValidator();
        var options = new DobbyOptions
        {
            Auth = new AuthOptions
            {
                PasswordKdfMode = "HumanApprovedPinRequired",
                LoginThrottleStepsSeconds = [0, 0, 0, 0, 0]
            }
        };

        var result = validator.Validate(Options.DefaultName, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("PasswordKdfMode", StringComparison.Ordinal));
        result.Failures.ShouldContain(x => x.Contains("LoginThrottleStepsSeconds", StringComparison.Ordinal));
    }
}
