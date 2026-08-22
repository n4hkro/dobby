using Dobby.Application;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Localization;
using Dobby.PluginFramework.Packaging;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class ShellPluginManagerOperationCoordinatorTests
{
    [Fact]
    public async Task Install_is_single_flight_cancellable_and_uses_one_monotonic_operation_identity()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporter = new BackgroundOperationReporter();
        var snapshots = new List<BackgroundOperationSnapshot>();
        reporter.SnapshotReported += snapshots.Add;
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        await using var coordinator = CreateCoordinator(
            shell,
            reporter,
            async (_, _, cancellationToken, progress) =>
            {
                progress.Report(new PluginPackageInstallProgress(
                    PluginPackageInstallStage.Reading,
                    25,
                    CanCancel: true));
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success(SuccessfulState());
            });

        var install = coordinator.InstallAsync("sample.dobbyplugin");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var competing = await coordinator.InstallAsync("other.dobbyplugin");

        competing.IsSuccess.ShouldBeFalse();
        competing.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        shell.PluginsManagerCancelVisibility.ShouldBe(System.Windows.Visibility.Visible);
        coordinator.CancelActiveOperation().ShouldBeTrue();
        var result = await install.WaitAsync(TimeSpan.FromSeconds(5));

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
        shell.PluginsManagerCancelVisibility.ShouldBe(System.Windows.Visibility.Collapsed);
        snapshots.Select(snapshot => snapshot.OperationId).Distinct().Count().ShouldBe(1);
        snapshots.Where(snapshot => snapshot.Progress.Percent.HasValue)
            .Select(snapshot => snapshot.Progress.Percent!.Value)
            .ShouldBeInOrder(SortDirection.Ascending);
        snapshots.Last().Status.ShouldBe(BackgroundOperationStatus.Cancelled);
        snapshots.Last().CanCancel.ShouldBeFalse();
    }

    [Fact]
    public async Task Finishing_stage_disables_cancellation_and_success_refreshes_once()
    {
        var finishing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporter = new BackgroundOperationReporter();
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        var refreshCount = 0;
        await using var coordinator = CreateCoordinator(
            shell,
            reporter,
            async (_, _, _, progress) =>
            {
                progress.Report(new PluginPackageInstallProgress(
                    PluginPackageInstallStage.Finishing,
                    90,
                    CanCancel: false));
                finishing.SetResult();
                await release.Task;
                return Result.Success(SuccessfulState());
            },
            () => refreshCount++);

        var install = coordinator.InstallAsync("sample.dobbyplugin");
        await finishing.Task.WaitAsync(TimeSpan.FromSeconds(5));

        coordinator.CancelActiveOperation().ShouldBeFalse();
        reporter.LatestSnapshot!.CanCancel.ShouldBeFalse();
        release.SetResult();
        (await install.WaitAsync(TimeSpan.FromSeconds(5))).IsSuccess.ShouldBeTrue();
        refreshCount.ShouldBe(1);
        reporter.LatestSnapshot!.Status.ShouldBe(BackgroundOperationStatus.Completed);
        reporter.LatestSnapshot.Progress.Percent.ShouldBe(100);
    }

    [Fact]
    public async Task Shutdown_cancels_and_drains_the_active_lifecycle_operation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = DefaultActions() with
        {
            DisablePluginAsync = async (_, _, cancellationToken) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success();
            }
        };
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        var coordinator = new ShellPluginManagerOperationCoordinator(
            shell,
            new BackgroundOperationReporter(),
            actions,
            () => { });

        var operation = coordinator.DisableAsync("com.dobby.sample");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.CancelAndDrainAsync().WaitAsync(TimeSpan.FromSeconds(5));

        (await operation).ErrorCode.ShouldBe(ErrorCode.Cancelled);
        (await coordinator.EnableAsync("com.dobby.sample")).IsSuccess.ShouldBeFalse();
        await coordinator.DisposeAsync();
    }

    private static ShellPluginManagerOperationCoordinator CreateCoordinator(
        ShellViewModel shell,
        BackgroundOperationReporter reporter,
        Func<string, CorrelationId, CancellationToken, IProgress<PluginPackageInstallProgress>, Task<Result<InstalledPluginState>>> install,
        Action? refresh = null)
    {
        return new ShellPluginManagerOperationCoordinator(
            shell,
            reporter,
            DefaultActions() with { InstallPackageAsync = install },
            refresh ?? (() => { }));
    }

    private static ShellPluginManagerRuntimeActions DefaultActions()
    {
        return new ShellPluginManagerRuntimeActions(
            (_, _, _, _) => Task.FromResult(Result.Success(SuccessfulState())),
            (_, _, _) => Task.FromResult(Result.Success()),
            (_, _, _) => Task.FromResult(Result.Success()),
            (_, _, _, _) => Task.FromResult(Result.Success()));
    }

    private static InstalledPluginState SuccessfulState()
    {
        var now = DateTimeOffset.Parse("2026-08-22T00:00:00Z");
        return new InstalledPluginState(
            "com.dobby.sample",
            "1.0.0",
            new string('a', 64),
            "Sample",
            "Sample plugin",
            IsEnabled: true,
            "verified",
            "runtime",
            "Enabled",
            now,
            now);
    }
}
