using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Messaging;
using Dobby.PluginFramework.Activation;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Activation;

public sealed class PostLoginPluginActivatorTests
{
    [Fact]
    public async Task Activation_returns_cancelled_outcome_when_session_token_is_already_cancelled()
    {
        var activator = NewActivator();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await activator.ActivateAsync([], new HostVersion(1, 0, 0), cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Completion.ShouldBe(PluginActivationCompletion.Cancelled);
        result.Value.Run.ActivatedPluginIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task V2_lifecycle_runs_in_order_with_host_cancellation_and_cleans_up_on_deactivation()
    {
        var module = new RecordingV2Module();
        await using var activator = NewActivator();
        using var authenticatedScope = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var activation = await activator.ActivateAsync(
        [
            new PluginActivationCandidate(
                "v2-plugin",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(module))
        ],
        new HostVersion(1, 0, 0),
        authenticatedScope.Token);

        activation.IsSuccess.ShouldBeTrue();
        activation.Value.Run.Outcomes.Single().Warning.ShouldBeNull();
        module.Calls.ShouldBe(["register", "initialize", "activate"]);
        module.AllLifecycleTokensWereCancelable.ShouldBeTrue();

        var deactivated = await activator.DeactivateAndUnloadAsync(
            "v2-plugin",
            authenticatedScope.Token);

        deactivated.IsSuccess.ShouldBeTrue();
        module.Calls.ShouldBe(["register", "initialize", "activate", "deactivate", "unload"]);
    }

    [Fact]
    public async Task V2_initialization_timeout_is_typed_and_runs_cleanup_lifecycle()
    {
        var module = new TimeoutInitializationV2Module();
        var options = new PluginLifecycleExecutionOptions(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
        await using var activator = new PostLoginPluginActivator(
            new PluginServiceRegistrationBridge(
                new PluginExtensionPointRegistry(),
                new PluginLifetimeManager()),
            options);
        using var authenticatedScope = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var activation = await activator.ActivateAsync(
        [
            new PluginActivationCandidate(
                "timeout-plugin",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(module))
        ],
        new HostVersion(1, 0, 0),
        authenticatedScope.Token);

        activation.IsSuccess.ShouldBeTrue();
        var outcome = activation.Value.Run.Outcomes.Single();
        outcome.Status.ShouldBe(PluginActivationStatus.Faulted);
        outcome.Reason.ShouldBe("lifecycle-initialize-timeout");
        module.DeactivateCalled.ShouldBeTrue();
        module.UnloadCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Activation_cancel_after_one_plugin_returns_partial_run_and_rolls_back_current_plugin()
    {
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(registry, lifetimes);
        var activator = new PostLoginPluginActivator(bridge);
        using var cts = new CancellationTokenSource();

        var result = await activator.ActivateAsync(
        [
            new PluginActivationCandidate(
                "active",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new SuccessfulModule())),
            new PluginActivationCandidate(
                "cancelled",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new CancelsAfterActivationModule(cts)))
        ],
        new HostVersion(1, 0, 0),
        cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Completion.ShouldBe(PluginActivationCompletion.Cancelled);
        result.Value.Run.ActivatedPluginIds.ShouldBe(["active"]);
    }

    [Fact]
    public void Activation_cascades_dependency_activation_failure_while_independent_plugins_continue()
    {
        var activator = NewActivator();
        var result = activator.Activate(
        [
            new PluginActivationCandidate(
                "core",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new FaultingModule())),
            new PluginActivationCandidate(
                "reports",
                "1.0.0",
                true,
                [new PluginDependency("core", "1.0.0")],
                _ => Result.Success<IPluginModule>(new SuccessfulModule())),
            new PluginActivationCandidate(
                "standalone",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new SuccessfulModule()))
        ],
        new HostVersion(1, 0, 0),
        CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ActivatedPluginIds.ShouldBe(["standalone"]);
        result.Value.Outcomes.Single(outcome => outcome.PluginId == "core").Status.ShouldBe(PluginActivationStatus.Faulted);
        result.Value.Outcomes.Single(outcome => outcome.PluginId == "reports").Reason
            .ShouldBe("dependency-activation-failed: core");
        result.Value.Outcomes.Single(outcome => outcome.PluginId == "standalone").Status.ShouldBe(PluginActivationStatus.Active);
        result.Value.Outcomes.Single(outcome => outcome.PluginId == "standalone").Warning
            .ShouldBe(PostLoginPluginActivator.LegacyV1UpgradeWarning);
    }

    [Fact]
    public void Activation_context_exposes_host_event_progress_and_cancellation_contracts()
    {
        var eventBus = new RecordingEventBus();
        var reporter = new RecordingBackgroundOperationReporter();
        var cancellation = new RecordingBackgroundOperationCancellation();
        var module = new ContextRecordingModule();
        var activator = NewActivator(new PluginHostServices(eventBus, reporter, cancellation));

        var result = activator.Activate(
        [
            new PluginActivationCandidate(
                "context-aware",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(module))
        ],
        new HostVersion(1, 0, 0),
        CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        module.Context.ShouldNotBeNull();
        module.Context.EventBus.ShouldNotBeNull();
        module.Context.BackgroundOperations.ShouldBeSameAs(reporter);
        module.Context.BackgroundOperationCancellation.ShouldBeSameAs(cancellation);
    }

    [Fact]
    public async Task Activation_context_event_handler_faults_are_attributed_to_plugin()
    {
        var eventBus = new InMemoryEventBus();
        var faults = new List<(string PluginId, Type EventType, ErrorCode ErrorCode)>();
        var activator = NewActivator(new PluginHostServices(
            eventBus,
            new RecordingBackgroundOperationReporter(),
            new RecordingBackgroundOperationCancellation(),
            pluginEventHandlerFaultSink: (pluginId, eventType, errorCode) =>
                faults.Add((pluginId, eventType, errorCode))));

        var result = activator.Activate(
        [
            new PluginActivationCandidate(
                "com.dobby.event-fault",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new EventFaultSubscriptionModule()))
        ],
        new HostVersion(1, 0, 0),
        CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await eventBus.PublishAsync(new SampleEvent("hello")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        faults.ShouldBe([("com.dobby.event-fault", typeof(SampleEvent), ErrorCode.PluginInternalError)]);
    }

    [Fact]
    public async Task Rollback_activated_plugins_cleans_up_in_reverse_order_and_unsubscribes_plugin_handlers()
    {
        FirstRollbackPage.Reset();
        SecondRollbackPage.Reset();
        var eventBus = new InMemoryEventBus();
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            registry,
            lifetimes,
            new PluginHostServices(
                eventBus,
                new RecordingBackgroundOperationReporter(),
                new RecordingBackgroundOperationCancellation()));
        var activator = new PostLoginPluginActivator(bridge);

        var activation = await activator.ActivateAsync(
        [
            new PluginActivationCandidate(
                "first",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new FirstRollbackModule())),
            new PluginActivationCandidate(
                "second",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new SecondRollbackModule()))
        ],
        new HostVersion(1, 0, 0),
        CancellationToken.None);

        activation.IsSuccess.ShouldBeTrue();
        activation.Value.Run.ActivatedPluginIds.ShouldBe(["first", "second"]);
        registry.GetRegistrationsSnapshot<IPluginPage>().Count.ShouldBe(2);

        await activator.RollbackActivatedPluginsAsync(activation.Value.Run.ActivatedPluginIds);

        registry.GetRegistrationsSnapshot<IPluginPage>().ShouldBeEmpty();
        FirstRollbackPage.DisposeOrder.ShouldBe(["second", "first"]);
        await eventBus.PublishAsync(new SampleEvent("hello")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });
        FirstRollbackPage.HandledPayloads.ShouldBeEmpty();
        SecondRollbackPage.HandledPayloads.ShouldBeEmpty();
    }

    [Fact]
    public async Task Retained_event_bus_cannot_subscribe_after_cleanup_and_reactivation_gets_a_fresh_working_bus()
    {
        var eventBus = new InMemoryEventBus();
        var hostServices = new PluginHostServices(
            eventBus,
            new RecordingBackgroundOperationReporter(),
            new RecordingBackgroundOperationCancellation(),
            pluginEventHandlerFaultSink: (_, _, _) => { });
        var registry = new PluginExtensionPointRegistry();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(registry, lifetimes, hostServices);
        var activator = new PostLoginPluginActivator(bridge);
        IEventBus? staleBus = null;
        IEventBus? freshBus = null;

        var firstActivation = await activator.ActivateAsync(
        [
            new PluginActivationCandidate(
                "reused-plugin",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new EventBusCapturingModule(bus => staleBus = bus)))
        ],
        new HostVersion(1, 0, 0),
        CancellationToken.None);

        firstActivation.IsSuccess.ShouldBeTrue();
        staleBus.ShouldNotBeNull();

        await activator.RollbackActivatedPluginsAsync(firstActivation.Value.Run.ActivatedPluginIds);

        var stalePayloads = new List<string>();
        using var staleSubscription = staleBus!.Subscribe<SampleEvent>((evt, _) =>
        {
            stalePayloads.Add(evt.Payload);
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new SampleEvent("stale")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        stalePayloads.ShouldBeEmpty();

        var secondActivation = await activator.ActivateAsync(
        [
            new PluginActivationCandidate(
                "reused-plugin",
                "1.0.0",
                true,
                [],
                _ => Result.Success<IPluginModule>(new EventBusCapturingModule(bus => freshBus = bus)))
        ],
        new HostVersion(1, 0, 0),
        CancellationToken.None);

        secondActivation.IsSuccess.ShouldBeTrue();
        freshBus.ShouldNotBeNull();

        var postReactivationStalePayloads = new List<string>();
        using var postReactivationStaleSubscription = staleBus.Subscribe<SampleEvent>((evt, _) =>
        {
            postReactivationStalePayloads.Add(evt.Payload);
            return Task.CompletedTask;
        });
        var freshPayloads = new List<string>();
        using var freshSubscription = freshBus!.Subscribe<SampleEvent>((evt, _) =>
        {
            freshPayloads.Add(evt.Payload);
            return Task.CompletedTask;
        });

        await eventBus.PublishAsync(new SampleEvent("fresh")
        {
            CorrelationId = CorrelationId.New(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        stalePayloads.ShouldBeEmpty();
        postReactivationStalePayloads.ShouldBeEmpty();
        freshPayloads.ShouldBe(["fresh"]);
    }

    private static PostLoginPluginActivator NewActivator(PluginHostServices? hostServices = null)
    {
        return new PostLoginPluginActivator(new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            new PluginLifetimeManager(),
            hostServices));
    }

    private sealed class FaultingModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class SuccessfulModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class RecordingV2Module : IPluginModuleV2
    {
        public List<string> Calls { get; } = [];

        public bool AllLifecycleTokensWereCancelable { get; private set; } = true;

        public void RegisterServices(IPluginServiceCollection services) => Calls.Add("register");

        public void OnActivated(IPluginActivationContext context) =>
            throw new InvalidOperationException("V1 activation must not run for a V2 module.");

        public ValueTask<Result> InitializeAsync(
            IPluginActivationContext context,
            CancellationToken cancellationToken)
        {
            Record("initialize", cancellationToken);
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> ActivateAsync(
            IPluginActivationContext context,
            CancellationToken cancellationToken)
        {
            Record("activate", cancellationToken);
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(CancellationToken cancellationToken)
        {
            Record("deactivate", cancellationToken);
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(CancellationToken cancellationToken)
        {
            Record("unload", cancellationToken);
            return ValueTask.FromResult(Result.Success());
        }

        private void Record(string call, CancellationToken cancellationToken)
        {
            Calls.Add(call);
            AllLifecycleTokensWereCancelable &= cancellationToken.CanBeCanceled;
        }
    }

    private sealed class TimeoutInitializationV2Module : IPluginModuleV2
    {
        public bool DeactivateCalled { get; private set; }

        public bool UnloadCalled { get; private set; }

        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context) =>
            throw new InvalidOperationException("V1 activation must not run for a V2 module.");

        public async ValueTask<Result> InitializeAsync(
            IPluginActivationContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result.Success();
        }

        public ValueTask<Result> ActivateAsync(
            IPluginActivationContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(CancellationToken cancellationToken)
        {
            DeactivateCalled = true;
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(CancellationToken cancellationToken)
        {
            UnloadCalled = true;
            return ValueTask.FromResult(Result.Success());
        }
    }

    private sealed class CancelsAfterActivationModule(CancellationTokenSource cancellation) : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
            cancellation.Cancel();
        }
    }

    private sealed class ContextRecordingModule : IPluginModule
    {
        public IPluginActivationContext? Context { get; private set; }

        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
            Context = context;
        }
    }

    private sealed class EventFaultSubscriptionModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
            context.EventBus.Subscribe<SampleEvent>((_, _) =>
                throw new InvalidOperationException("raw plugin event detail"));
        }
    }

    private sealed class EventBusCapturingModule(Action<IEventBus> capture) : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
        }

        public void OnActivated(IPluginActivationContext context)
        {
            capture(context.EventBus);
        }
    }

    private sealed class FirstRollbackModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, FirstRollbackPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
            context.EventBus.Subscribe<SampleEvent>((evt, _) =>
            {
                FirstRollbackPage.HandledPayloads.Add(evt.Payload);
                return Task.CompletedTask;
            });
        }
    }

    private sealed class SecondRollbackModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, SecondRollbackPage>();
        }

        public void OnActivated(IPluginActivationContext context)
        {
            context.EventBus.Subscribe<SampleEvent>((evt, _) =>
            {
                SecondRollbackPage.HandledPayloads.Add(evt.Payload);
                return Task.CompletedTask;
            });
        }
    }

    private sealed class FirstRollbackPage : IPluginPage, IDisposable
    {
        public static List<string> DisposeOrder { get; } = [];

        public static List<string> HandledPayloads { get; } = [];

        public PluginPageDescriptor Descriptor { get; } = new("first.page", "first.page", "first.page.label");

        public static void Reset()
        {
            DisposeOrder.Clear();
            HandledPayloads.Clear();
        }

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Failure<IPluginPageModel>(ErrorCode.NotFound));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public void Dispose()
        {
            DisposeOrder.Add("first");
        }
    }

    private sealed class SecondRollbackPage : IPluginPage, IDisposable
    {
        public static List<string> HandledPayloads { get; } = [];

        public PluginPageDescriptor Descriptor { get; } = new("second.page", "second.page", "second.page.label");

        public static void Reset()
        {
            HandledPayloads.Clear();
        }

        public ValueTask<Result<IPluginPageModel>> LoadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Failure<IPluginPageModel>(ErrorCode.NotFound));

        public ValueTask<Result> ActivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> DeactivateAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public ValueTask<Result> UnloadAsync(
            PluginPageContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(Result.Success());

        public void Dispose()
        {
            FirstRollbackPage.DisposeOrder.Add("second");
        }
    }

    private sealed record SampleEvent(string Payload) : PublicIntegrationEvent;

    private sealed class RecordingEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : PublicIntegrationEvent
        {
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : PublicIntegrationEvent
        {
            return new Subscription();
        }

        private sealed class Subscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class RecordingBackgroundOperationReporter : IBackgroundOperationReporter
    {
        public IProgress<BackgroundOperationSnapshot> Progress { get; } = new Progress<BackgroundOperationSnapshot>();
    }

    private sealed class RecordingBackgroundOperationCancellation : IBackgroundOperationCancellation
    {
        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }
}
