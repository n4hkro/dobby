using Dobby.Host.Plugins;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class PluginInstallConcurrencyGateTests
{
    [Fact]
    public async Task Same_plugin_operations_are_serialized_at_the_plugin_gate()
    {
        var gate = new PluginInstallConcurrencyGate(maximumConcurrentOperations: 2);
        using var first = gate.Enter("com.dobby.same", CancellationToken.None);
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Task.Factory.StartNew(
            () =>
            {
                using var lease = gate.Enter("com.dobby.same", CancellationToken.None);
                acquired.SetResult(true);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        SpinWait.SpinUntil(
            () => gate.Snapshot("com.dobby.same").WaitingForPlugin == 1,
            TimeSpan.FromSeconds(5)).ShouldBeTrue();
        gate.Snapshot("com.dobby.same").ActiveForPlugin.ShouldBe(1);
        acquired.Task.IsCompleted.ShouldBeFalse();

        first.Dispose();

        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await second.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Different_plugins_run_in_parallel_but_total_capacity_is_bounded()
    {
        var gate = new PluginInstallConcurrencyGate(maximumConcurrentOperations: 2);
        using var first = gate.Enter("com.dobby.one", CancellationToken.None);
        using var second = gate.Enter("com.dobby.two", CancellationToken.None);
        gate.Snapshot("com.dobby.one").ActiveForPlugin.ShouldBe(1);
        gate.Snapshot("com.dobby.two").ActiveForPlugin.ShouldBe(1);

        var thirdAcquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var third = Task.Factory.StartNew(
            () =>
            {
                using var lease = gate.Enter("com.dobby.three", CancellationToken.None);
                thirdAcquired.SetResult(true);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        SpinWait.SpinUntil(
            () => gate.Snapshot("com.dobby.three").WaitingForCapacity == 1,
            TimeSpan.FromSeconds(5)).ShouldBeTrue();
        thirdAcquired.Task.IsCompleted.ShouldBeFalse();

        first.Dispose();

        await thirdAcquired.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await third.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
