using Dobby.Configuration;
using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class KdfPolicyResolverTests
{
    [Fact]
    public void Resolve_returns_pbkdf2_fallback_for_default_policy()
    {
        var resolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });

        var result = resolver.Resolve();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(KdfPolicy.SkeletonPbkdf2Fallback);
    }

    [Theory]
    [InlineData("Argon2id")]
    [InlineData("Unknown")]
    public void Resolve_fails_closed_for_unsupported_modes_without_argon2_amendment(string mode)
    {
        var resolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = mode });

        var result = resolver.Resolve();

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
    }
}
