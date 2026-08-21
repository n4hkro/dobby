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

    [Fact]
    public void Implemented_password_kdf_runtime_value_is_allowed()
    {
        var result = Validate(new DobbyOptions
        {
            Auth = new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }
        });

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Argon2id")]
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
    public void Supported_password_kdf_catalog_contains_only_runtime_selectable_implementations()
    {
        DobbyOptionsValidator.SupportedPasswordKdfModes.ShouldBe(["Pbkdf2Sha256"]);
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

    [Fact]
    public void Durable_throttle_and_verifier_defaults_are_bounded()
    {
        var auth = new AuthOptions();

        auth.LoginThrottleFailureWindowSeconds.ShouldBe(900);
        auth.LoginGlobalFailureThreshold.ShouldBe(10);
        auth.LoginGlobalBackoffSeconds.ShouldBe(60);
        auth.MaxConcurrentPasswordVerifications.ShouldBe(2);
    }

    [Theory]
    [InlineData(59, 10, 60, 2)]
    [InlineData(900, 4, 60, 2)]
    [InlineData(900, 10, 9, 2)]
    [InlineData(900, 10, 60, 0)]
    public void Durable_throttle_and_verifier_values_fail_closed_outside_bounds(
        int windowSeconds,
        int globalThreshold,
        int globalBackoffSeconds,
        int maxConcurrent)
    {
        var result = Validate(new DobbyOptions
        {
            Auth = new AuthOptions
            {
                LoginThrottleFailureWindowSeconds = windowSeconds,
                LoginGlobalFailureThreshold = globalThreshold,
                LoginGlobalBackoffSeconds = globalBackoffSeconds,
                MaxConcurrentPasswordVerifications = maxConcurrent
            }
        });

        result.Failed.ShouldBeTrue();
    }

    private static ValidateOptionsResult Validate(DobbyOptions options)
    {
        return new DobbyOptionsValidator().Validate(Options.DefaultName, options);
    }
}
