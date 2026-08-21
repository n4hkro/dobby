using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class PasswordPolicyValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_missing_or_whitespace_only_passwords(string? password)
    {
        var result = PasswordPolicyValidator.Validate(password);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("correct horse battery staple")]
    [InlineData("Şifrə 密碼 🔐")]
    public void Validate_accepts_non_empty_passwords_up_to_policy_limit(string password)
    {
        PasswordPolicyValidator.Validate(password).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_accepts_one_hundred_twenty_eight_characters()
    {
        PasswordPolicyValidator.Validate(new string('a', 128)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_rejects_one_hundred_twenty_nine_characters()
    {
        var result = PasswordPolicyValidator.Validate(new string('a', 129));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }
}
