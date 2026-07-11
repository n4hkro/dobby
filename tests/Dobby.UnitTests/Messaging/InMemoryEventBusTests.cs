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

    [Fact]
    public async Task Event_bus_propagates_host_handler_faults()
    {
        var bus = new InMemoryEventBus();
        var received = new List<string>();
        using var throwing = bus.Subscribe<SampleEvent>((_, _) =>
            throw new InvalidOperationException("host subscriber failed"));
        using var recording = bus.Subscribe<SampleEvent>((evt, _) =>
        {
            received.Add(evt.Payload);
            return Task.CompletedTask;
        });

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            bus.PublishAsync(new SampleEvent("hello")
            {
                CorrelationId = CorrelationId.New(),
                OccurredAt = DateTimeOffset.UtcNow
            }));

        exception.Message.ShouldBe("host subscriber failed");
        received.ShouldBeEmpty();
    }

    [Fact]
    public async Task Event_bus_isolates_handler_faults_only_when_fault_sink_is_enabled()
    {
        var faults = new List<(Type EventType, ErrorCode ErrorCode)>();
        var bus = new InMemoryEventBus((eventType, errorCode) => faults.Add((eventType, errorCode)));
        var received = new List<string>();
        using var throwing = bus.Subscribe<SampleEvent>((_, _) =>
            throw new InvalidOperationException("raw plugin event detail"));
        using var recording = bus.Subscribe<SampleEvent>((evt, _) =>
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
        faults.ShouldBe([(typeof(SampleEvent), ErrorCode.PluginInternalError)]);
    }

    private sealed record SampleEvent(string Payload) : PublicIntegrationEvent;
}
