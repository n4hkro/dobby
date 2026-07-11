using System.Windows;
using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Licensing;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.IntegrationTests.Shell;

public sealed class LicenseImportShellSurfaceTests
{
    [Fact]
    public async Task Plugins_license_surface_invokes_file_picker_and_coordinator()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: true,
            PluginRuntimeMode.ReadOnlyExpired);
        var picker = new RecordingLicenseFilePicker("selected.dobbylicense");
        var coordinator = RecordingLicenseImportCoordinator.CompletesWith(
            LicenseImportResult.Success(LicenseLifecycleState.Active, "plugin.sample"));
        LicenseImportResult? refreshedImport = null;
        var action = new LicenseImportShellAction(
            shell,
            picker,
            coordinator,
            result =>
            {
                refreshedImport = result;
                return ValueTask.CompletedTask;
            });

        var outcome = await action.ImportAsync("owner", CorrelationId.New());

        outcome.ShouldBe(LicenseImportShellOutcome.Imported);
        picker.PickCalls.ShouldBe(1);
        coordinator.ImportCalls.ShouldBe(1);
        coordinator.Paths.ShouldBe(["selected.dobbylicense"]);
        refreshedImport.ShouldNotBeNull();
        refreshedImport.PluginId.ShouldBe("plugin.sample");
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxal edildi");
        shell.LicenseImportIsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Successful_import_is_not_relabelled_rejected_when_post_import_refresh_fails()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetLicenseSurface(true, true, PluginRuntimeMode.ReadOnlyExpired);
        var unexpectedFailures = new List<ErrorCode>();
        var action = new LicenseImportShellAction(
            shell,
            new RecordingLicenseFilePicker("selected.dobbylicense"),
            RecordingLicenseImportCoordinator.CompletesWith(
                LicenseImportResult.Success(LicenseLifecycleState.Active, "plugin.sample")),
            _ => new ValueTask(Task.FromException(new InvalidOperationException("refresh-failed"))),
            unexpectedFailures.Add);

        var outcome = await action.ImportAsync("owner", CorrelationId.New());

        outcome.ShouldBe(LicenseImportShellOutcome.Imported);
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxal edildi");
        shell.LicenseImportIsEnabled.ShouldBeTrue();
        unexpectedFailures.ShouldBe([ErrorCode.PluginInternalError]);
    }

    [Fact]
    public async Task License_import_action_rejects_duplicate_click_while_busy()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetLicenseSurface(true, true, PluginRuntimeMode.ReadOnlyExpired);
        var picker = new RecordingLicenseFilePicker("selected.dobbylicense");
        var coordinator = RecordingLicenseImportCoordinator.Blocking();
        var action = new LicenseImportShellAction(shell, picker, coordinator);

        var first = action.ImportAsync("owner", CorrelationId.New());
        await coordinator.ImportStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var duplicate = await action.ImportAsync("owner", CorrelationId.New());

        duplicate.ShouldBe(LicenseImportShellOutcome.Busy);
        coordinator.ImportCalls.ShouldBe(1);
        picker.PickCalls.ShouldBe(1);
        shell.LicenseImportIsEnabled.ShouldBeFalse();
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxal olunur");

        coordinator.Complete(LicenseImportResult.Success(LicenseLifecycleState.Active, "plugin.sample"));
        (await first).ShouldBe(LicenseImportShellOutcome.Imported);
        shell.LicenseImportIsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task License_import_action_enforces_permission_before_picker()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetLicenseSurface(true, false, PluginRuntimeMode.ReadOnlyExpired);
        var picker = new RecordingLicenseFilePicker("selected.dobbylicense");
        var coordinator = RecordingLicenseImportCoordinator.CompletesWith(
            LicenseImportResult.Success(LicenseLifecycleState.Active, "plugin.sample"));
        var action = new LicenseImportShellAction(shell, picker, coordinator);

        var outcome = await action.ImportAsync("managed", CorrelationId.New());

        outcome.ShouldBe(LicenseImportShellOutcome.PermissionDenied);
        picker.PickCalls.ShouldBe(0);
        coordinator.ImportCalls.ShouldBe(0);
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxalı üçün icazə yoxdur");
    }

    [Fact]
    public async Task License_import_action_maps_rejection_to_safe_localized_feedback()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetLicenseSurface(true, true, PluginRuntimeMode.ReadOnlyExpired);
        var coordinator = RecordingLicenseImportCoordinator.CompletesWith(
            LicenseImportResult.Failure(
                LicenseRejectReasons.BindingMismatch,
                "customer-a/deployment-a"));
        var action = new LicenseImportShellAction(
            shell,
            new RecordingLicenseFilePicker("selected.dobbylicense"),
            coordinator);

        var outcome = await action.ImportAsync("owner", CorrelationId.New());

        outcome.ShouldBe(LicenseImportShellOutcome.Rejected);
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxalı qəbul edilmədi");
        shell.LicenseFeedbackText.ShouldNotContain("customer-a");
        shell.LicenseFeedbackText.ShouldNotContain("deployment-a");
    }

    [Fact]
    public async Task License_import_action_maps_unexpected_failure_to_safe_localized_feedback()
    {
        var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
        shell.SetLicenseSurface(true, true, PluginRuntimeMode.ReadOnlyExpired);
        var unexpectedFailures = new List<ErrorCode>();
        var action = new LicenseImportShellAction(
            shell,
            new RecordingLicenseFilePicker("selected.dobbylicense"),
            new ThrowingLicenseImportCoordinator(),
            unexpectedFailureSink: unexpectedFailures.Add);

        var outcome = await action.ImportAsync("owner", CorrelationId.New());

        outcome.ShouldBe(LicenseImportShellOutcome.Rejected);
        shell.LicenseFeedbackText.ShouldBe("Lisenziya idxalı qəbul edilmədi");
        shell.LicenseFeedbackText.ShouldNotContain("database-secret");
        shell.LicenseImportIsEnabled.ShouldBeTrue();
        unexpectedFailures.ShouldBe([ErrorCode.PluginInternalError]);
    }

    [Fact]
    public void Plugins_license_surface_separates_status_view_from_import_action()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var shell = ShellViewModel.CreateDefault(strings);

        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: false,
            PluginRuntimeMode.ReadOnlyExpired,
            feedbackKey: HostResourceKeys.LicenseImportRejected,
            pluginId: "plugin.sample",
            pluginName: "Sample plugin");

        shell.LicensePanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();

        shell.LicensePanelVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseStatusVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseListVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseDetailVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseImportVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseRuntimeModeText.ShouldBe(strings.GetString(HostResourceKeys.LicenseModeReadOnlyExpired));
        shell.LicenseListText.ShouldBe("Sample plugin (plugin.sample)");
        shell.LicenseDetailText.ShouldNotContain("default");
        shell.LicenseDetailText.ShouldNotContain("revision");
        shell.LicenseFeedbackText.ShouldBe(strings.GetString(HostResourceKeys.LicenseImportRejected));

        shell.SetLicenseSurface(
            canViewStatus: false,
            canImport: true,
            PluginRuntimeMode.Denied);

        shell.LicensePanelVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseStatusVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseListVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseDetailVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicenseImportVisibility.ShouldBe(Visibility.Visible);
        shell.LicenseImportIsEnabled.ShouldBeTrue();
        shell.LicenseImportLabel.ShouldBe(strings.GetString(HostResourceKeys.LicenseImportAction));
    }

    [Fact]
    public void Host_shell_wires_license_import_action_on_plugins_surface_and_keeps_about_clean()
    {
        var root = RepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));
        var windowCode = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var aboutModel = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "Settings", "AboutSettingsModel.cs"));

        xaml.ShouldContain("x:Name=\"LicensePanel\"");
        xaml.ShouldContain("x:Name=\"LicenseImportButton\"");
        xaml.ShouldContain("Click=\"LicenseImport_Click\"");
        xaml.ShouldContain("Visibility=\"{Binding LicenseImportVisibility}\"");
        xaml.ShouldContain("IsEnabled=\"{Binding LicenseImportIsEnabled}\"");
        windowCode.ShouldContain("new WindowsLicenseFilePicker()");
        windowCode.ShouldContain("LicenseImportShellAction");
        aboutModel.ShouldContain("HasLicenseImportAction => false");
    }

    private sealed class RecordingLicenseFilePicker(string? selectedPath) : ILicenseFilePicker
    {
        public int PickCalls { get; private set; }

        public string? PickLicenseFile()
        {
            PickCalls++;
            return selectedPath;
        }
    }

    private sealed class RecordingLicenseImportCoordinator : ILicenseImportFileCoordinator
    {
        private readonly LicenseImportResult? immediateResult;
        private readonly TaskCompletionSource<LicenseImportResult>? completion;

        private RecordingLicenseImportCoordinator(
            LicenseImportResult? immediateResult,
            TaskCompletionSource<LicenseImportResult>? completion)
        {
            this.immediateResult = immediateResult;
            this.completion = completion;
        }

        public int ImportCalls { get; private set; }

        public List<string> Paths { get; } = [];

        public TaskCompletionSource<bool> ImportStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static RecordingLicenseImportCoordinator CompletesWith(LicenseImportResult result)
        {
            return new RecordingLicenseImportCoordinator(result, null);
        }

        public static RecordingLicenseImportCoordinator Blocking()
        {
            return new RecordingLicenseImportCoordinator(
                null,
                new TaskCompletionSource<LicenseImportResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public Task<LicenseImportResult> ImportFileAsync(
            string actorId,
            string path,
            CorrelationId correlationId,
            CancellationToken cancellationToken = default)
        {
            ImportCalls++;
            Paths.Add(path);
            ImportStarted.TrySetResult(true);
            return completion?.Task ?? Task.FromResult(immediateResult!);
        }

        public void Complete(LicenseImportResult result)
        {
            completion!.TrySetResult(result);
        }
    }

    private sealed class ThrowingLicenseImportCoordinator : ILicenseImportFileCoordinator
    {
        public Task<LicenseImportResult> ImportFileAsync(
            string actorId,
            string path,
            CorrelationId correlationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<LicenseImportResult>(
                new InvalidOperationException("database-secret"));
        }
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
