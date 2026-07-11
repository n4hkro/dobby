using Dobby.Shell.WorkSessions;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class WorkSessionLifecycleTests
{
    [Fact]
    public void Minimize_restore_retains_progress_result_error_input_and_screen_state()
    {
        var registry = new WorkSessionRegistry();
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");

        registry.UpdateProgress(session.SessionId, WorkSessionProgress.Determinate(64));
        registry.RecordResult(session.SessionId, "Dobby.Work.Result.Ready");
        registry.RecordError(session.SessionId, "Dobby.Work.Error.Retryable");
        registry.RecordInputState(session.SessionId, "search", "Əliyev");
        registry.RecordScreenState(session.SessionId, "scroll-offset", "240");
        registry.Minimize(session.SessionId);
        registry.Restore(session.SessionId);

        var restored = registry.Get(session.SessionId);
        restored.ShouldNotBeNull();
        restored.IsMinimized.ShouldBeFalse();
        restored.Progress.Percent.ShouldBe(64);
        restored.ResultSummaryKey.ShouldBe("Dobby.Work.Result.Ready");
        restored.ErrorSummaryKey.ShouldBe("Dobby.Work.Error.Retryable");
        restored.InputState["search"].ShouldBe("Əliyev");
        restored.ScreenState["scroll-offset"].ShouldBe("240");
    }

    [Fact]
    public void Close_coordinator_supports_cancel_close_and_minimize_warning_choices()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");

        var request = coordinator.RequestClose(session.SessionId, WorkSessionCloseReason.Navigation);

        request.Choices.ShouldBe([
            WorkSessionCloseChoice.Cancel,
            WorkSessionCloseChoice.Minimize,
            WorkSessionCloseChoice.Close
        ]);

        coordinator.ApplyChoice(request, WorkSessionCloseChoice.Cancel).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId).ShouldNotBeNull();

        coordinator.ApplyChoice(request, WorkSessionCloseChoice.Minimize).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId)!.IsMinimized.ShouldBeTrue();

        coordinator.ApplyChoice(request, WorkSessionCloseChoice.Close).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId).ShouldBeNull();
    }

    [Fact]
    public void Navigation_targets_every_non_minimized_session_for_the_displayed_route_only()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var unrelatedOlder = registry.Open("plugin.other", "plugin/other/work", "Dobby.Plugin.Other.Work");
        var currentFirst = registry.Open("plugin.sample", "plugin/sample/current", "Dobby.Plugin.Sample.First");
        var currentSecond = registry.Open("plugin.sample", "plugin/sample/current", "Dobby.Plugin.Sample.Second");
        var currentMinimized = registry.Open("plugin.sample", "plugin/sample/current", "Dobby.Plugin.Sample.Minimized");
        registry.Minimize(currentMinimized.SessionId);

        var pending = coordinator.RequestNavigation("plugin/sample/current", "plugin/sample/destination");

        pending.ShouldNotBeNull();
        pending.RequestedRouteKey.ShouldBe("plugin/sample/destination");
        pending.CloseRequest.SessionIds.Count.ShouldBe(2);
        pending.CloseRequest.SessionIds.ShouldContain(currentFirst.SessionId);
        pending.CloseRequest.SessionIds.ShouldContain(currentSecond.SessionId);
        pending.CloseRequest.SessionIds.ShouldNotContain(unrelatedOlder.SessionId);
        pending.CloseRequest.SessionIds.ShouldNotContain(currentMinimized.SessionId);

        coordinator.ApplyChoice(pending.CloseRequest, WorkSessionCloseChoice.Minimize).IsSuccess.ShouldBeTrue();
        registry.Get(unrelatedOlder.SessionId)!.IsMinimized.ShouldBeFalse();
        registry.Get(currentFirst.SessionId)!.IsMinimized.ShouldBeTrue();
        registry.Get(currentSecond.SessionId)!.IsMinimized.ShouldBeTrue();
    }

    [Fact]
    public void Lock_preserves_active_and_minimized_work_sessions_for_restore()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var active = registry.Open("owner-1", "plugin.sample.active", "Dobby.Plugin.Sample.Active");
        var minimized = registry.Open("owner-1", "plugin.sample.minimized", "Dobby.Plugin.Sample.Minimized");
        registry.UpdateProgress(active.SessionId, WorkSessionProgress.Determinate(75));
        registry.RecordInputState(active.SessionId, "query", "Şəki");
        registry.RecordScreenState(active.SessionId, "selected-tab", "results");
        registry.Minimize(minimized.SessionId);

        var preserved = coordinator.HandleLock();

        var preservedSessionIds = preserved.Select(session => session.SessionId).ToArray();
        preservedSessionIds.Length.ShouldBe(2);
        preservedSessionIds.ShouldContain(active.SessionId);
        preservedSessionIds.ShouldContain(minimized.SessionId);
        registry.Get(active.SessionId).ShouldNotBeNull();
        registry.Get(minimized.SessionId).ShouldNotBeNull();
        registry.Get(active.SessionId)!.IsMinimized.ShouldBeFalse();
        registry.Get(minimized.SessionId)!.IsMinimized.ShouldBeTrue();
        registry.Get(active.SessionId)!.Progress.Percent.ShouldBe(75);
        registry.Get(active.SessionId)!.InputState["query"].ShouldBe("Şəki");
        registry.Get(active.SessionId)!.ScreenState["selected-tab"].ShouldBe("results");
    }

    [Fact]
    public void Logout_returns_controlled_close_request_without_silent_session_loss()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");

        var pending = coordinator.HandleLogout();

        pending.ShouldNotBeNull();
        pending.Action.ShouldBe(PendingWorkSessionAction.Logout);
        pending.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.Logout);
        pending.CloseRequest.Choices.ShouldBe([
            WorkSessionCloseChoice.Cancel,
            WorkSessionCloseChoice.Close
        ]);
        registry.Get(session.SessionId).ShouldNotBeNull();

        coordinator.ApplyChoice(pending.CloseRequest, WorkSessionCloseChoice.Close).IsSuccess.ShouldBeTrue();
        registry.Get(session.SessionId).ShouldBeNull();
    }

    [Fact]
    public void Logout_close_request_targets_every_active_and_minimized_work_session()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var active = registry.Open("owner-1", "plugin.sample.active", "Dobby.Plugin.Sample.Active");
        var minimized = registry.Open("owner-1", "plugin.sample.minimized", "Dobby.Plugin.Sample.Minimized");
        registry.Minimize(minimized.SessionId);

        var pending = coordinator.HandleLogout();

        pending.ShouldNotBeNull();
        pending.CloseRequest.SessionIds.Count.ShouldBe(2);
        pending.CloseRequest.SessionIds.ShouldContain(active.SessionId);
        pending.CloseRequest.SessionIds.ShouldContain(minimized.SessionId);
        pending.CloseRequest.Choices.ShouldBe([
            WorkSessionCloseChoice.Cancel,
            WorkSessionCloseChoice.Close
        ]);

        coordinator.ApplyChoice(pending.CloseRequest, WorkSessionCloseChoice.Close).IsSuccess.ShouldBeTrue();
        registry.Get(active.SessionId).ShouldBeNull();
        registry.Get(minimized.SessionId).ShouldBeNull();
    }

    [Fact]
    public void Logout_returns_controlled_close_request_for_minimized_work_sessions()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        registry.Minimize(session.SessionId);

        var pending = coordinator.HandleLogout();

        pending.ShouldNotBeNull();
        pending.Action.ShouldBe(PendingWorkSessionAction.Logout);
        pending.CloseRequest.SessionId.ShouldBe(session.SessionId);
        pending.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.Logout);
        pending.CloseRequest.Choices.ShouldNotContain(WorkSessionCloseChoice.Minimize);
        registry.Get(session.SessionId).ShouldNotBeNull();
        registry.Get(session.SessionId)!.IsMinimized.ShouldBeTrue();
    }

    [Fact]
    public void Plugin_lifecycle_returns_controlled_close_requests_without_silent_session_loss()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var disableSession = registry.Open("plugin.sample", "plugin.sample.disable", "Dobby.Plugin.Sample.Disable");
        var uninstallSession = registry.Open("plugin.other", "plugin.other.uninstall", "Dobby.Plugin.Other.Uninstall");

        var disable = coordinator.RequestPluginDisable("plugin.sample");
        var uninstall = coordinator.RequestPluginUninstall("plugin.other");

        disable.ShouldNotBeNull();
        disable.Action.ShouldBe(PendingWorkSessionAction.PluginDisable);
        disable.ActionTargetId.ShouldBe("plugin.sample");
        disable.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.PluginDisable);
        disable.CloseRequest.Choices.ShouldBe([
            WorkSessionCloseChoice.Cancel,
            WorkSessionCloseChoice.Close
        ]);
        uninstall.ShouldNotBeNull();
        uninstall.Action.ShouldBe(PendingWorkSessionAction.PluginUninstall);
        uninstall.ActionTargetId.ShouldBe("plugin.other");
        uninstall.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.PluginUninstall);
        uninstall.CloseRequest.Choices.ShouldBe([
            WorkSessionCloseChoice.Cancel,
            WorkSessionCloseChoice.Close
        ]);
        registry.Get(disableSession.SessionId).ShouldNotBeNull();
        registry.Get(uninstallSession.SessionId).ShouldNotBeNull();

        coordinator.ApplyChoice(disable.CloseRequest, WorkSessionCloseChoice.Cancel).IsSuccess.ShouldBeTrue();
        coordinator.ApplyChoice(uninstall.CloseRequest, WorkSessionCloseChoice.Close).IsSuccess.ShouldBeTrue();
        registry.Get(disableSession.SessionId).ShouldNotBeNull();
        registry.Get(uninstallSession.SessionId).ShouldBeNull();
    }

    [Fact]
    public void Plugin_lifecycle_close_request_targets_every_session_owned_by_the_plugin()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var active = registry.Open("plugin.sample", "plugin.sample.active", "Dobby.Plugin.Sample.Active");
        var minimized = registry.Open("plugin.sample", "plugin.sample.minimized", "Dobby.Plugin.Sample.Minimized");
        var other = registry.Open("plugin.other", "plugin.other.active", "Dobby.Plugin.Other.Active");
        registry.Minimize(minimized.SessionId);

        var pending = coordinator.RequestPluginDisable("plugin.sample");

        pending.ShouldNotBeNull();
        pending.CloseRequest.SessionIds.Count.ShouldBe(2);
        pending.CloseRequest.SessionIds.ShouldContain(active.SessionId);
        pending.CloseRequest.SessionIds.ShouldContain(minimized.SessionId);
        pending.CloseRequest.SessionIds.ShouldNotContain(other.SessionId);

        coordinator.ApplyChoice(pending.CloseRequest, WorkSessionCloseChoice.Close).IsSuccess.ShouldBeTrue();
        registry.Get(active.SessionId).ShouldBeNull();
        registry.Get(minimized.SessionId).ShouldBeNull();
        registry.Get(other.SessionId).ShouldNotBeNull();
    }

    [Fact]
    public void Plugin_lifecycle_matches_owner_only_when_another_session_route_collides_with_plugin_id()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var owned = registry.Open("plugin.sample", "plugin/sample/work", "Dobby.Plugin.Sample.Work");
        var routeCollision = registry.Open("plugin.other", "plugin.sample", "Dobby.Plugin.Other.Work");

        var pending = coordinator.RequestPluginDisable("plugin.sample");

        pending.ShouldNotBeNull();
        pending.CloseRequest.SessionIds.ShouldBe([owned.SessionId]);
        pending.CloseRequest.SessionIds.ShouldNotContain(routeCollision.SessionId);
    }

    [Fact]
    public void Plugin_lifecycle_returns_controlled_close_requests_for_minimized_work_sessions()
    {
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var disableSession = registry.Open("plugin.sample", "plugin.sample.disable", "Dobby.Plugin.Sample.Disable");
        var uninstallSession = registry.Open("plugin.other", "plugin.other.uninstall", "Dobby.Plugin.Other.Uninstall");
        registry.Minimize(disableSession.SessionId);
        registry.Minimize(uninstallSession.SessionId);

        var disable = coordinator.RequestPluginDisable("plugin.sample");
        var uninstall = coordinator.RequestPluginUninstall("plugin.other");

        disable.ShouldNotBeNull();
        disable.Action.ShouldBe(PendingWorkSessionAction.PluginDisable);
        disable.CloseRequest.SessionId.ShouldBe(disableSession.SessionId);
        disable.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.PluginDisable);
        disable.CloseRequest.Choices.ShouldNotContain(WorkSessionCloseChoice.Minimize);
        uninstall.ShouldNotBeNull();
        uninstall.Action.ShouldBe(PendingWorkSessionAction.PluginUninstall);
        uninstall.CloseRequest.SessionId.ShouldBe(uninstallSession.SessionId);
        uninstall.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.PluginUninstall);
        uninstall.CloseRequest.Choices.ShouldNotContain(WorkSessionCloseChoice.Minimize);
        registry.Get(disableSession.SessionId)!.IsMinimized.ShouldBeTrue();
        registry.Get(uninstallSession.SessionId)!.IsMinimized.ShouldBeTrue();
    }

    [Fact]
    public void Work_sessions_are_not_restored_by_a_new_registry_after_restart()
    {
        var registry = new WorkSessionRegistry();
        registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");

        new WorkSessionRegistry().Snapshot().ShouldBeEmpty();
    }
}
