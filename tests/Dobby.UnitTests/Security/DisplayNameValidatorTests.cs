using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class DisplayNameValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_returns_null_for_omitted_or_blank_display_name(string? input)
    {
        DisplayNameValidator.Normalize(input).ShouldBeNull();
    }

    [Fact]
    public void Normalize_trims_and_preserves_local_unicode_display_name()
    {
        var normalized = DisplayNameValidator.Normalize("  Əli Məmmədov  ");

        normalized.ShouldBe("Əli Məmmədov");
    }

    [Fact]
    public void Normalize_does_not_apply_username_canonicalization_rules()
    {
        var normalized = DisplayNameValidator.Normalize("  OWNER Şəxs  ");

        normalized.ShouldBe("OWNER Şəxs");
    }
}
