using Dobby.Host.Login;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LoginViewModelThrottleStateTests
{
    [Fact]
    public void Login_button_disables_only_during_countdown_and_reenables_after_zero()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var throttle = new LoginThrottleService([0, 0, 10, 30, 60], time);
        var viewModel = new LoginViewModel(null, throttle);

        throttle.RecordFailure("owner");
        viewModel.RefreshThrottleState();
        viewModel.IsLoginEnabled.ShouldBeTrue();
        throttle.RecordFailure("owner");
        viewModel.RefreshThrottleState();
        viewModel.IsLoginEnabled.ShouldBeTrue();
        throttle.RecordFailure("owner");
        viewModel.RefreshThrottleState();
        viewModel.IsLoginEnabled.ShouldBeFalse();
        viewModel.ThrottleCountdownText.ShouldBe("10");

        time.Advance(TimeSpan.FromSeconds(10));
        viewModel.RefreshThrottleState();

        viewModel.IsLoginEnabled.ShouldBeTrue();
        viewModel.ThrottleCountdownText.ShouldBeNull();
    }

    [Fact]
    public void Remembered_account_reads_its_durable_throttle_bucket_after_restart()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var store = new InMemoryLoginThrottleStore();
        var firstRuntime = new LoginThrottleService([0, 0, 10, 30, 60], time, store);
        firstRuntime.RecordFailure("user-1");
        firstRuntime.RecordFailure("user-1");
        firstRuntime.RecordFailure("user-1");
        var restartedRuntime = new LoginThrottleService([0, 0, 10, 30, 60], time, store);
        var viewModel = new LoginViewModel(
            new RememberedLoginAccount("user-1", "owner"),
            restartedRuntime);

        viewModel.RefreshThrottleState();

        viewModel.IsLoginEnabled.ShouldBeFalse();
        viewModel.ThrottleCountdownText.ShouldBe("10");
    }
}
