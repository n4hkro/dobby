using Dobby.Application;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Host;
using Dobby.Localization;
using Dobby.Messaging.UiDispatch;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class ShellBackgroundOperationCoordinatorTests
{
    [Fact]
    public void Background_operation_updates_are_posted_through_shell_ui_dispatcher()
    {
        var reporter = new BackgroundOperationReporter();
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        var dispatcher = new RecordingUiDispatcher();
        var first = Snapshot(25);
        reporter.Progress.Report(first);

        var coordinator = new ShellBackgroundOperationCoordinator(shell, reporter, dispatcher);
        coordinator.Attach();

        dispatcher.PostedActions.Count.ShouldBe(1);
        shell.BackgroundOperationPercent.ShouldBe(0);

        dispatcher.RunPostedActions();

        shell.BackgroundOperationPercent.ShouldBe(25);
        shell.BackgroundOperationIsIndeterminate.ShouldBeFalse();

        reporter.Progress.Report(Snapshot(80));
        dispatcher.PostedActions.Count.ShouldBe(1);
        shell.BackgroundOperationPercent.ShouldBe(25);

        dispatcher.RunPostedActions();

        shell.BackgroundOperationPercent.ShouldBe(80);
    }

    [Fact]
    public void Plugin_owned_background_operation_reaches_shell_through_official_reporter()
    {
        var reporter = new BackgroundOperationReporter();
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        var dispatcher = new RecordingUiDispatcher();
        var coordinator = new ShellBackgroundOperationCoordinator(shell, reporter, dispatcher);
        coordinator.Attach();

        reporter.Progress.Report(new BackgroundOperationSnapshot(
            Guid.NewGuid(),
            BackgroundOperationOwnerKind.Plugin,
            "com.dobby.sample",
            "Dobby.Plugin.Sample.Import",
            ShortDetailKey: null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Indeterminate(),
            CanCancel: false,
            Guid.NewGuid(),
            FailureReason: null));
        dispatcher.RunPostedActions();

        shell.BackgroundOperationText.ShouldBe("[[Dobby.Plugin.Sample.Import]]");
        shell.BackgroundOperationIsIndeterminate.ShouldBeTrue();
    }

    private static BackgroundOperationSnapshot Snapshot(int percent)
    {
        return new BackgroundOperationSnapshot(
            Guid.NewGuid(),
            BackgroundOperationOwnerKind.Host,
            null,
            HostResourceKeys.ShellStatusReady,
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Determinate(percent),
            CanCancel: false,
            Guid.NewGuid(),
            FailureReason: null);
    }

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public bool IsOnUiThread => false;

        public List<Action> PostedActions { get; } = [];

        public Task InvokeAsync(Func<Task> action)
        {
            Post(() => action().GetAwaiter().GetResult());
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> action)
        {
            Post(() => action().GetAwaiter().GetResult());
            return Task.FromResult(default(T)!);
        }

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            PostedActions.Add(action);
        }

        public void RunPostedActions()
        {
            var actions = PostedActions.ToArray();
            PostedActions.Clear();
            foreach (var action in actions)
            {
                action();
            }
        }
    }
}
