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
    public void Work_sessions_are_not_restored_by_a_new_registry_after_restart()
    {
        var registry = new WorkSessionRegistry();
        registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");

        new WorkSessionRegistry().Snapshot().ShouldBeEmpty();
    }
}
