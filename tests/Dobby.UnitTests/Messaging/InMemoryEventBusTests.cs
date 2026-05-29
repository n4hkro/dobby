using Dobby.Contracts.Events;
using Dobby.Core;
using Dobby.Messaging;
using Shouldly;

namespace Dobby.UnitTests.Messaging;

public sealed class InMemoryEventBusTests
{
    [Fact]
    public async Task Event_bus_publishes_only_public_integration_events_to_subscribers()
    {
        var bus = new InMemoryEventBus();
        var received = new List<string>();
        using var subscription = bus.Subscribe<SampleEvent>((evt, _) =>
        {
            received.Add(evt.Payload);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new SampleEvent("hello")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        received.ShouldBe(["hello"]);
    }

    private sealed record SampleEvent(string Payload) : PublicIntegrationEvent;
}
