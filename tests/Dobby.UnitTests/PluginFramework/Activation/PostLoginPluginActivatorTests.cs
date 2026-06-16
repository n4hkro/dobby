using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.PluginFramework;
using Dobby.Core;
using Dobby.Core.Versioning;
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
        module.Context.EventBus.ShouldBeSameAs(eventBus);
        module.Context.BackgroundOperations.ShouldBeSameAs(reporter);
        module.Context.BackgroundOperationCancellation.ShouldBeSameAs(cancellation);
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
