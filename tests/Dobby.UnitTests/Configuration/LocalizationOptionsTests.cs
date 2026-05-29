using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class LocalizationOptionsTests
{
    [Fact]
    public void Supported_cultures_default_to_ADR_0043_set()
    {
        new LocalizationOptions().SupportedCultures.ShouldBe(["az-AZ", "en-US", "tr-TR"]);
    }

    [Fact]
    public void Default_culture_is_az_AZ_and_must_remain_supported()
    {
        var result = new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions
            {
                Localization = new LocalizationOptions
                {
                    DefaultCulture = "en-US",
                    SupportedCultures = ["en-US", "tr-TR"]
                }
            });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("DefaultCulture", StringComparison.Ordinal));
        result.Failures.ShouldContain(x => x.Contains("SupportedCultures", StringComparison.Ordinal));
    }
}
