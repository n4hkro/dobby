using System.Threading;
using Dobby.Host.Startup;
using Shouldly;

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
        using var first = new SingleInstanceCoordinator(mutexName, activationEventName);
        using var second = new SingleInstanceCoordinator(mutexName, activationEventName);

        first.Acquire().IsFirstInstance.ShouldBeTrue();
        second.Acquire().IsFirstInstance.ShouldBeFalse();
        second.HandOffToFirstInstance();

        activation.WaitOne(TimeSpan.FromSeconds(1)).ShouldBeTrue();
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
}
