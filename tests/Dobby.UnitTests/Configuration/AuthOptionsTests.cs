using Dobby.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Dobby.UnitTests.Configuration;

public sealed class AuthOptionsTests
{
    [Fact]
    public void Login_throttle_steps_default_to_ADR_0027_floor()
    {
        new AuthOptions().LoginThrottleStepsSeconds.ShouldBe([0, 0, 10, 30, 60]);
    }

    [Theory]
    [InlineData("Argon2id")]
    [InlineData("Pbkdf2Sha256")]
    public void Password_kdf_runtime_values_are_allowed(string mode)
    {
        var result = Validate(new DobbyOptions
        {
            Auth = new AuthOptions { PasswordKdfMode = mode }
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("HumanApprovedPinRequired")]
    [InlineData("Sha1")]
    public void Password_kdf_stop_condition_and_unknown_values_fail_closed(string mode)
    {
        var result = Validate(new DobbyOptions
        {
            Auth = new AuthOptions { PasswordKdfMode = mode }
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("PasswordKdfMode", StringComparison.Ordinal));
    }

    [Fact]
    public void Login_throttle_steps_cannot_be_lowered_below_floor()
    {
        var result = Validate(new DobbyOptions
        {
            Auth = new AuthOptions { LoginThrottleStepsSeconds = [0, 0, 9, 30, 60] }
        });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("LoginThrottleStepsSeconds", StringComparison.Ordinal));
    }

    private static ValidateOptionsResult Validate(DobbyOptions options)
    {
        return new DobbyOptionsValidator().Validate(Options.DefaultName, options);
    }
}
