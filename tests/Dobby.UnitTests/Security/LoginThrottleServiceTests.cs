using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class LoginThrottleServiceTests
{
    [Fact]
    public void Failed_attempts_follow_configured_floor_and_countdown_expires_by_time()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService([0, 0, 10, 30, 60], time);

        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(0);
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(0);
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(10);
        throttle.Check().RetryAfterSeconds.ShouldBe(10);

        time.Advance(TimeSpan.FromSeconds(9));
        throttle.Check().RetryAfterSeconds.ShouldBe(1);
        time.Advance(TimeSpan.FromSeconds(1));
        throttle.Check().RetryAfterSeconds.ShouldBe(0);

        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(30);
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(60);
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(60);
    }

    [Fact]
    public void Account_failures_are_isolated_below_the_device_global_threshold()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService([0, 0, 10, 30, 60], time);

        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner");

        throttle.Check("owner").RetryAfterSeconds.ShouldBe(10);
        throttle.Check("other").RetryAfterSeconds.ShouldBe(0);
    }

    [Fact]
    public void Device_bucket_blocks_account_spraying_at_the_explicit_threshold()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService(
            [0, 0, 10, 30, 60],
            time,
            deviceFailureThreshold: 5,
            deviceBackoffSeconds: 60);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            throttle.RecordFailure($"account-{attempt}");
        }

        throttle.Check("unrelated-account").RetryAfterSeconds.ShouldBe(60);
    }

    [Fact]
    public void Successful_login_resets_failed_attempts_and_countdown()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService([0, 0, 10, 30, 60], time);
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(10);

        throttle.RecordSuccess("owner");

        throttle.Check().RetryAfterSeconds.ShouldBe(0);
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(0);
    }

    [Fact]
    public void Failure_window_expiry_starts_a_fresh_account_sequence()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService(
            [0, 0, 10, 30, 60],
            time,
            failureWindowSeconds: 60);
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(10);

        time.Advance(TimeSpan.FromSeconds(60));

        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(0);
    }

    [Fact]
    public void Store_failure_fails_closed_without_throwing_or_starting_authentication()
    {
        var throttle = new LoginThrottleService(
            [0, 0, 10, 30, 60],
            TimeProvider.System,
            new FailingLoginThrottleStore());

        var result = throttle.Check("owner");

        result.IsAvailable.ShouldBeFalse();
        result.IsThrottled.ShouldBeTrue();
    }

    private sealed class FailingLoginThrottleStore : ILoginThrottleStore
    {
        public T ExecuteAtomically<T>(Func<ILoginThrottleBucketSession, T> action)
        {
            throw new InvalidOperationException("simulated failed commit");
        }
    }
}
