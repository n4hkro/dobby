using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class SecurityTests
{
    [Fact]
    public void Operation_key_parser_splits_on_last_dot()
    {
        var key = OperationKey.Parse("com.dobby.sample.greet.execute");

        key.IsSuccess.ShouldBeTrue();
        key.Value.TargetId.ShouldBe("com.dobby.sample.greet");
        key.Value.Verb.ShouldBe(OperationVerb.Execute);
    }

    [Fact]
    public void Operation_key_parser_rejects_unknown_verbs()
    {
        var key = OperationKey.Parse("com.dobby.sample.greet.fly");

        key.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Pbkdf2_password_hasher_uses_floor_policy_and_verifies_password()
    {
        var hasher = new Pbkdf2PasswordHasher(KdfPolicy.SkeletonPbkdf2Fallback);

        var hash = hasher.Hash("correct horse battery staple");

        hash.Iterations.ShouldBeGreaterThanOrEqualTo(600_000);
        hash.Salt.Length.ShouldBeGreaterThanOrEqualTo(16);
        hasher.Verify("correct horse battery staple", hash).ShouldBeTrue();
        hasher.Verify("wrong", hash).ShouldBeFalse();
    }
}
