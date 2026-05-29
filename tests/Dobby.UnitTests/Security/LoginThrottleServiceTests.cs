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
    public void Username_changes_do_not_reset_process_wide_throttle_state()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService([0, 0, 10, 30, 60], time);

        throttle.RecordFailure("owner");
        throttle.RecordFailure("other");
        var result = throttle.RecordFailure("third");

        result.RetryAfterSeconds.ShouldBe(10);
    }

    [Fact]
    public void Successful_login_resets_failed_attempts_and_countdown()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService([0, 0, 10, 30, 60], time);
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner");
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(10);

        throttle.RecordSuccess();

        throttle.Check().RetryAfterSeconds.ShouldBe(0);
        throttle.RecordFailure("owner").RetryAfterSeconds.ShouldBe(0);
    }
}
