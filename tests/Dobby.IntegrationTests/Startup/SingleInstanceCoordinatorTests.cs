using System.Threading;
using Dobby.Host.Startup;
using Shouldly;
using System.Runtime.ExceptionServices;

namespace Dobby.IntegrationTests.Startup;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void Second_instance_hands_off_activation_without_acquiring_first_instance_slot()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = "Local\\Dobby.Tests." + suffix;
        var activationEventName = "Local\\Dobby.Tests.Activate." + suffix;
        using var activation = new EventWaitHandle(false, EventResetMode.AutoReset, activationEventName);
        using var first = new SingleInstanceCoordinator(mutexName, activationEventName, TimeSpan.Zero);
        using var second = new SingleInstanceCoordinator(mutexName, activationEventName, TimeSpan.Zero);

        first.Acquire().IsFirstInstance.ShouldBeTrue();
        AcquireOnSeparateThread(second).IsFirstInstance.ShouldBeFalse();
        second.HandOffToFirstInstance();

        activation.WaitOne(TimeSpan.FromSeconds(1)).ShouldBeTrue();
    }

    [Fact]
    public void First_instance_can_listen_for_second_instance_activation_requests()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = "Local\\Dobby.Tests." + suffix;
        var activationEventName = "Local\\Dobby.Tests.Activate." + suffix;
        using var first = new SingleInstanceCoordinator(mutexName, activationEventName, TimeSpan.Zero);
        using var second = new SingleInstanceCoordinator(mutexName, activationEventName, TimeSpan.Zero);
        using var activationReceived = new ManualResetEventSlim(false);
        var activationCount = 0;

        first.Acquire().IsFirstInstance.ShouldBeTrue();
        using var listener = first.StartActivationListener(() =>
        {
            Interlocked.Increment(ref activationCount);
            activationReceived.Set();
        });
        AcquireOnSeparateThread(second).IsFirstInstance.ShouldBeFalse();

        second.HandOffToFirstInstance();

        activationReceived.Wait(TimeSpan.FromSeconds(1)).ShouldBeTrue();
        activationCount.ShouldBe(1);
    }

    [Fact]
    public void Releasing_first_instance_allows_next_coordinator_to_acquire_slot()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = "Local\\Dobby.Tests." + suffix;
        var activationEventName = "Local\\Dobby.Tests.Activate." + suffix;
        using (var first = new SingleInstanceCoordinator(mutexName, activationEventName))
        {
            first.Acquire().IsFirstInstance.ShouldBeTrue();
        }

        using var next = new SingleInstanceCoordinator(mutexName, activationEventName);

        next.Acquire().IsFirstInstance.ShouldBeTrue();
    }

    [Fact]
    public void Instance_started_while_first_instance_is_exiting_waits_and_acquires_slot()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = "Local\\Dobby.Tests." + suffix;
        var activationEventName = "Local\\Dobby.Tests.Activate." + suffix;
        var first = new SingleInstanceCoordinator(mutexName, activationEventName);
        using var next = new SingleInstanceCoordinator(mutexName, activationEventName);
        SingleInstanceAcquireResult? result = null;
        Exception? exception = null;
        var firstDisposed = false;
        var acquireStarted = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                acquireStarted.Set();
                result = next.Acquire();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        try
        {
            first.Acquire().IsFirstInstance.ShouldBeTrue();
            thread.Start();
            acquireStarted.Wait(TimeSpan.FromSeconds(1)).ShouldBeTrue();
            Thread.Sleep(50);
            first.Dispose();
            firstDisposed = true;
        }
        finally
        {
            if (!firstDisposed)
            {
                first.Dispose();
            }
        }

        thread.Join(TimeSpan.FromSeconds(2)).ShouldBeTrue();
        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        result.ShouldNotBeNull();
        result.IsFirstInstance.ShouldBeTrue();
    }

    private static SingleInstanceAcquireResult AcquireOnSeparateThread(SingleInstanceCoordinator coordinator)
    {
        SingleInstanceAcquireResult? result = null;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = coordinator.Acquire();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        return result ?? throw new InvalidOperationException("Acquire did not return a result.");
    }

    [Fact]
    public void Existing_unowned_mutex_is_treated_as_available_first_instance_slot()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = "Local\\Dobby.Tests." + suffix;
        var activationEventName = "Local\\Dobby.Tests.Activate." + suffix;
        using var staleHandle = new Mutex(initiallyOwned: false, mutexName);
        using var next = new SingleInstanceCoordinator(mutexName, activationEventName);

        next.Acquire().IsFirstInstance.ShouldBeTrue();
    }
}
