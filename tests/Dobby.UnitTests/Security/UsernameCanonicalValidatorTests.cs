using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class UsernameCanonicalValidatorTests
{
    [Theory]
    [InlineData("owner", "owner")]
    [InlineData(" Owner-1.Test ", "owner-1.test")]
    [InlineData("abc_xyz-09.test", "abc_xyz-09.test")]
    public void Normalize_accepts_allowed_ascii_and_lowercases(string input, string expected)
    {
        var result = UsernameCanonicalValidator.Normalize(input);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Normalize_rejects_missing_or_too_short_usernames(string? input)
    {
        var result = UsernameCanonicalValidator.Normalize(input);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void Normalize_rejects_usernames_longer_than_sixty_four_characters()
    {
        var result = UsernameCanonicalValidator.Normalize(new string('a', 65));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }

    [Theory]
    [InlineData("şah")]
    [InlineData("owner name")]
    [InlineData("owner@example")]
    public void Normalize_rejects_unicode_whitespace_and_symbols_outside_allowed_set(string input)
    {
        var result = UsernameCanonicalValidator.Normalize(input);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void Normalize_rejects_existing_normalized_username_conflicts()
    {
        var existing = new HashSet<string>(StringComparer.Ordinal) { "owner" };

        var result = UsernameCanonicalValidator.Normalize(" OWNER ", existing);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.AlreadyExists);
    }
}
