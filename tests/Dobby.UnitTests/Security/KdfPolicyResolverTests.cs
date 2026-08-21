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
        result.Value.ShouldSatisfyAllConditions(
            policy => policy.Algorithm.ShouldBe("PBKDF2-HMAC-SHA256"),
            policy => policy.Iterations.ShouldBe(600_000),
            policy => policy.SaltBytes.ShouldBe(16),
            policy => policy.HashBytes.ShouldBe(32));
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

    [Fact]
    public void Every_configuration_accepted_kdf_has_a_runtime_implementation()
    {
        foreach (var mode in DobbyOptionsValidator.SupportedPasswordKdfModes)
        {
            var validation = new DobbyOptionsValidator().Validate(
                Microsoft.Extensions.Options.Options.DefaultName,
                new DobbyOptions { Auth = new AuthOptions { PasswordKdfMode = mode } });
            var resolved = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = mode }).Resolve();

            validation.Succeeded.ShouldBeTrue();
            resolved.IsSuccess.ShouldBeTrue();
        }
    }
}
