using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class ProductOptionsTests
{
    [Fact]
    public void Production_requires_real_https_support_contact_and_legal_line()
    {
        var result = Validate(new ProductOptions
        {
            SupportContact = "support@example.invalid",
            LegalLine = "TODO"
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(failure => failure.Contains("Product.SupportContact", StringComparison.Ordinal));
        result.Failures.ShouldContain(failure => failure.Contains("Product.LegalLine", StringComparison.Ordinal));
    }

    [Fact]
    public void Repository_support_endpoint_and_legal_line_are_production_valid()
    {
        var result = Validate(new ProductOptions
        {
            SupportContact = "https://github.com/n4hkro/dobby/issues",
            LegalLine = "Copyright © 2026 Dobby contributors",
            ArtifactMode = ProductArtifactMode.Installed
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_artifact_mode_is_rejected()
    {
        var result = Validate(new ProductOptions
        {
            SupportContact = "https://github.com/n4hkro/dobby/issues",
            LegalLine = "Copyright © 2026 Dobby contributors",
            ArtifactMode = (ProductArtifactMode)999
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(failure => failure.Contains("ArtifactMode", StringComparison.Ordinal));
    }

    private static ValidateOptionsResult Validate(ProductOptions product)
    {
        return new DobbyOptionsValidator().Validate(
            Options.DefaultName,
            new DobbyOptions { Product = product });
    }
}
