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
        var result = DisplayNameValidator.Normalize(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void Normalize_trims_and_preserves_local_unicode_display_name()
    {
        var normalized = DisplayNameValidator.Normalize("  Əli Məmmədov  ");

        normalized.IsSuccess.ShouldBeTrue();
        normalized.Value.ShouldBe("Əli Məmmədov");
    }

    [Fact]
    public void Normalize_does_not_apply_username_canonicalization_rules()
    {
        var normalized = DisplayNameValidator.Normalize("  OWNER Şəxs  ");

        normalized.IsSuccess.ShouldBeTrue();
        normalized.Value.ShouldBe("OWNER Şəxs");
    }

    [Fact]
    public void Normalize_uses_unicode_form_c_without_losing_local_characters()
    {
        var result = DisplayNameValidator.Normalize("  A\u030Aysel  ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Åysel");
    }

    [Theory]
    [InlineData("Ali\u0000Veli")]
    [InlineData("Ali\nVeli")]
    [InlineData("Ali\u202EVeli")]
    [InlineData("Ali\u2066Veli")]
    public void Normalize_rejects_control_and_bidirectional_format_characters(string input)
    {
        var result = DisplayNameValidator.Normalize(input);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(Dobby.Core.ErrorCode.ValidationFailed);
    }

    [Fact]
    public void Normalize_enforces_bounded_utf16_length_after_normalization()
    {
        DisplayNameValidator.Normalize(new string('ə', 128)).IsSuccess.ShouldBeTrue();
        DisplayNameValidator.Normalize(new string('ə', 129)).ErrorCode
            .ShouldBe(Dobby.Core.ErrorCode.ValidationFailed);
    }
}
