using Dobby.Core;
using Dobby.Host;
using Dobby.Host.Login;
using System.IO;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LoginWindowRuntimeWiringTests
{
    [Fact]
    public void App_creates_login_runtime_and_passes_workflow_to_login_window()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var preparer = ReadSource("src", "Dobby.Host", "Login", "LoginRuntimePreparer.cs");
        var presenter = ReadSource("src", "Dobby.Host", "PostLoginShellPresenter.cs");
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        app.ShouldContain("new LoginWindow(startupSnapshot)");
        app.ShouldContain("loginWindow.ContentRendered +=");
        app.ShouldContain("LoginRuntimePreparer.InitializeLoginEntryRuntimeAsync(loginWindow)");
        app.ShouldContain("loginWindow.AttachDeferredWorkflow(");
        preparer.ShouldContain("PreparedLoginEntryRuntime");
        preparer.ShouldContain("PreLoginRuntime");
        preparer.ShouldContain("HostLoginRuntimeFactory.CreatePreLogin(");
        preparer.ShouldContain("HostLoginRuntimeFactory.CreateAuthenticated(");
        preparer.ShouldContain("new ShellPluginWorkSessionService(workSessionRegistry)");
        preparer.ShouldContain("loginWindow.AttachRuntime(preLoginRuntime)");
        presenter.ShouldContain("new MainWindow(shellViewModel)");
        presenter.ShouldContain("window.AttachRuntime(");
        app.ShouldContain("loginWindow.AuthenticatedSession");
        app.ShouldContain("LoginRuntimePreparer.InitializeAuthenticatedRuntimeAsync(");
        mainWindow.ShouldContain("loginRuntime.PostLoginSessionRuntime");
        mainWindow.ShouldNotContain("loginRuntime.SelfServicePasswordChangeCoordinator");

        var showDialog = app.IndexOf("loginWindow.ShowDialog()", StringComparison.Ordinal);
        var presenterCall = app.IndexOf("PostLoginShellPresenter.ShowAsync(", StringComparison.Ordinal);
        showDialog.ShouldBeGreaterThanOrEqualTo(0);
        presenterCall.ShouldBeGreaterThan(showDialog);
    }

    [Fact]
    public void App_reopens_login_when_post_login_session_requests_login_return()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");
        var presenter = ReadSource("src", "Dobby.Host", "PostLoginShellPresenter.cs");

        app.ShouldContain("while (true)");
        app.ShouldContain("returnToLogin");
        presenter.ShouldContain("window.ShowDialog()");
        presenter.ShouldContain("window.ReturnToLoginRequested");
        mainWindow.ShouldContain("public bool ReturnToLoginRequested");
        mainWindow.ShouldContain("ReturnToLoginRequested = true");
        mainWindow.ShouldNotContain("result.ErrorCode != ErrorCode.Cancelled");
    }

    [Fact]
    public void App_and_host_login_runtime_use_explicit_async_cleanup()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var preparer = ReadSource("src", "Dobby.Host", "Login", "LoginRuntimePreparer.cs");
        var presenter = ReadSource("src", "Dobby.Host", "PostLoginShellPresenter.cs");
        var runtime = ReadSource("src", "Dobby.Host", "Login", "HostLoginRuntime.cs");

        app.ShouldContain("protected override async void OnStartup");
        app.ShouldContain("await RunLoginLoopAsync(firstLoginStartupTiming)");
        app.ShouldContain("LoginRuntimePreparer.ObserveWhenReadyWithoutBlockingShutdown(preparedLoginEntryRuntimeTask)");
        preparer.ShouldContain("await preparedRuntime.DisposeAsync()");
        preparer.ShouldContain("_ = ObserveWhenReadyAsync(preparedRuntimeTask)");
        presenter.ShouldContain("await using var preparedRuntimeLease = preparedLoginRuntime.ConfigureAwait(false)");
        presenter.ShouldContain("authenticatedLifetime.Cancel()");
        presenter.ShouldContain("await warmupTask.ConfigureAwait(false)");
        runtime.ShouldContain("IAsyncDisposable");
        runtime.ShouldContain("PostLoginSessionRuntime.Dispose();");
        runtime.ShouldContain("await PluginRuntimeCoordinator.DisposeAsync()");
        runtime.IndexOf("PostLoginSessionRuntime.Dispose();", StringComparison.Ordinal)
            .ShouldBeLessThan(runtime.IndexOf("await PluginRuntimeCoordinator.DisposeAsync()", StringComparison.Ordinal));
    }

    [Fact]
    public void Main_window_records_host_input_as_session_activity_without_blocking_input()
    {
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        mainWindow.ShouldContain("PreviewKeyDown");
        mainWindow.ShouldContain("PreviewMouseDown");
        mainWindow.ShouldContain("RecordSessionActivity");
        mainWindow.ShouldContain("QueueSessionActivityRecord(");
        mainWindow.ShouldContain("Task.Run(() => RecordSessionActivityCore(runtime))");
        mainWindow.ShouldContain("private static Result RecordSessionActivityCore(PostLoginSessionRuntime runtime)");
        mainWindow.ShouldContain("Result.Failure(ErrorCode.DatabaseUnavailable)");
        mainWindow.ShouldNotContain("private void RecordSessionActivity()\r\n    {\r\n        if (sessionRuntime is null || session is null)\r\n        {\r\n            return;\r\n        }\r\n\r\n        sessionRuntime.RecordActivity();\r\n    }");
    }

    [Fact]
    public void Main_window_custom_constructor_uses_supplied_shell_view_model_without_throwaway_default_model()
    {
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        CountOccurrences(mainWindow, "ShellViewModel.CreateDefault").ShouldBe(1);
        mainWindow.ShouldContain(": this(CreateDefaultShellViewModel())");
        mainWindow.ShouldContain("private static ShellViewModel CreateDefaultShellViewModel()");
        mainWindow.ShouldContain(": this(shellViewModel, actionOverrides)");
        mainWindow.ShouldNotContain(": this()\r\n    {\r\n        SetShellViewModel(shellViewModel);");
    }

    [Fact]
    public void Main_window_exposes_account_password_change_and_manual_lock_surface()
    {
        var xaml = ReadSource("src", "Dobby.Host", "MainWindow.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        xaml.ShouldContain("CurrentPasswordBox");
        xaml.ShouldContain("NewPasswordBox");
        xaml.ShouldContain("ConfirmNewPasswordBox");
        xaml.ShouldContain("SettingsCurrentPasswordBox");
        xaml.ShouldContain("SettingsNewPasswordBox");
        xaml.ShouldContain("SettingsConfirmNewPasswordBox");
        xaml.ShouldContain("PasswordChangeRememberMeCheckBox");
        xaml.ShouldContain("SettingsPasswordChangeRememberMeCheckBox");
        xaml.ShouldContain("ChangePasswordButton");
        xaml.ShouldContain("SettingsChangePasswordButton");
        xaml.ShouldContain("ManualLockButton");
        codeBehind.ShouldContain("ChangePassword_Click");
        codeBehind.ShouldContain("SettingsChangePassword_Click");
        codeBehind.ShouldContain("loginRuntime.AccountSettingsModel.ChangePassword");
        codeBehind.ShouldContain("currentPasswordBox.Password");
        codeBehind.ShouldContain("confirmationBox.Password");
        codeBehind.ShouldContain("ManualLock_Click");
        codeBehind.ShouldContain("sessionRuntime.LockNow");
    }

    [Fact]
    public void Login_window_refreshes_throttle_countdown_until_button_is_reenabled()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldContain("DispatcherTimer");
        codeBehind.ShouldContain("throttleTimer.Tick");
        codeBehind.ShouldContain("StartThrottleTimer");
        codeBehind.ShouldContain("StopThrottleTimer");
    }

    [Fact]
    public void Login_window_submits_to_runtime_workflow_before_accepting_dialog()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldContain("workflow.Submit");
        codeBehind.IndexOf("workflow.Submit", StringComparison.Ordinal)
            .ShouldBeLessThan(codeBehind.IndexOf("DialogResult = true", StringComparison.Ordinal));
        codeBehind.ShouldNotContain("private void Continue_Click(object sender, RoutedEventArgs e)\r\n    {\r\n        DialogResult = true;\r\n    }");
    }

    [Fact]
    public void Login_window_runs_authentication_work_off_the_ui_click_handler()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldContain("private async void Continue_Click");
        codeBehind.ShouldContain("await Task.Run(() => workflow.Submit(");
        codeBehind.ShouldContain("LoginButton.IsEnabled = false");
        codeBehind.ShouldContain("LoginButton.IsEnabled = loginWorkflow is not null && viewModel.IsLoginEnabled");
    }

    [Fact]
    public void Login_window_does_not_start_input_driven_credential_verification()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");
        var workflow = ReadSource("src", "Dobby.Host", "Login", "LoginRuntimeWorkflow.cs");
        var deferredWorkflow = ReadSource("src", "Dobby.Host", "Login", "DeferredLoginRuntimeWorkflow.cs");

        codeBehind.ShouldNotContain("CredentialWarmup");
        codeBehind.ShouldNotContain("ScheduleCredentialWarmup");
        codeBehind.ShouldNotContain("QueueCredentialWarmup");
        codeBehind.ShouldNotContain("viewModel.Password");
        workflow.ShouldNotContain("CredentialWarmup");
        workflow.ShouldNotContain("WarmedCredential");
        workflow.ShouldNotContain("LoginWithValidatedCredential(");
        deferredWorkflow.ShouldNotContain("CredentialWarmup");
    }

    [Fact]
    public void Login_window_can_render_before_runtime_is_attached()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");
        var xaml = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml");

        codeBehind.ShouldContain("public void AttachDeferredWorkflow(ILoginRuntimeWorkflow runtimeWorkflow)");
        codeBehind.ShouldContain("public void AttachRuntime(HostPreLoginRuntime runtime)");
        codeBehind.ShouldContain("LoginButton.IsEnabled = loginWorkflow is not null && viewModel.IsLoginEnabled");
        codeBehind.ShouldContain("RecoveryButton.IsEnabled = loginWorkflow is not null");
        codeBehind.ShouldContain("ApplyInitialState();");
        xaml.ShouldContain("TextChanged=\"UsernameBox_TextChanged\"");
        codeBehind.ShouldContain("userEditedLoginFields");
        codeBehind.ShouldContain("ApplyInitialState(preserveLoginFields: true)");
        codeBehind.ShouldContain("ShouldKeepRenderedLoginFields(runtimeViewModel)");
    }

    [Fact]
    public void Login_window_first_paint_path_never_restores_or_resolves_a_password()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldNotContain("DataContext = viewModel");
        codeBehind.ShouldNotContain("PasswordBox.GetType()");
        codeBehind.ShouldNotContain("InitialPasswordDisplayValue");
        codeBehind.ShouldNotContain("SubmittedPassword(");
        codeBehind.ShouldNotContain("ApplyInitialPassword");
        codeBehind.ShouldNotContain("PasswordBoxPasswordProperty");
        codeBehind.ShouldContain("PasswordBox.Password");
    }

    [Fact]
    public void Login_window_keeps_matching_startup_prefill_rendered_when_runtime_attaches()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldContain("ShouldKeepRenderedLoginFields(runtimeViewModel)");
        codeBehind.ShouldContain("ApplyRuntimeStateWithoutLoginFieldReset()");
        codeBehind.ShouldContain("StringComparer.Ordinal.Equals(viewModel.Username, runtimeViewModel.Username)");
        codeBehind.ShouldContain("viewModel.RememberMeSelected == runtimeViewModel.RememberMeSelected");
        codeBehind.ShouldNotContain("IsPasswordPrefilled");
        codeBehind.ShouldContain("if (shouldKeepRenderedLoginFields)");
    }

    [Fact]
    public void Login_window_snapshot_constructor_defers_recovery_runtime_until_full_runtime_attach()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldContain(": this(null, startupSnapshot.CreateLoginViewModel(), recoveryViewModel: null)");
        codeBehind.ShouldContain("private RecoveryViewModel? recoveryViewModel;");
        codeBehind.ShouldContain("if (loginWorkflow is null || recoveryViewModel is null)");
        codeBehind.ShouldNotContain("CreateDefaultRecoveryViewModel()");
    }

    [Fact]
    public void Login_window_exposes_display_name_input_for_clean_install_owner_bootstrap()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        xaml.ShouldContain("DisplayNameBox");
        xaml.ShouldContain("Dobby.Host.Login.DisplayName");
        codeBehind.ShouldContain("viewModel.ShouldShowDisplayNameInput");
        codeBehind.ShouldContain("DisplayNameLabel.Visibility");
        codeBehind.ShouldContain("DisplayNameBox.Visibility");
        codeBehind.ShouldContain("SubmittedDisplayName()");
    }

    [Fact]
    public void Login_window_wires_password_edit_without_explicit_forget_saved_login_action()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        xaml.ShouldContain("PasswordChanged=\"PasswordBox_PasswordChanged\"");
        xaml.ShouldNotContain(string.Concat("Forget", "Saved", "Login", "Button"));
        xaml.ShouldNotContain(string.Concat("Login", "Forget", "Saved", "Login"));
        codeBehind.ShouldContain("RememberMeCheckBox.IsChecked = viewModel.RememberMeSelected");
        codeBehind.ShouldContain("viewModel.EditPassword(PasswordBox.Password)");
        codeBehind.ShouldNotContain(string.Concat("Forget", "Saved", "Login"));
    }

    [Fact]
    public void Lock_window_submits_unlock_and_logout_to_runtime_workflow()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LockWindow.xaml.cs");
        var viewModel = ReadSource("src", "Dobby.Host", "Login", "LockViewModel.cs");
        var unlockHandler = codeBehind[
            codeBehind.IndexOf("private void Unlock_Click", StringComparison.Ordinal)..codeBehind.IndexOf("private void Logout_Click", StringComparison.Ordinal)];
        var logoutHandler = codeBehind[
            codeBehind.IndexOf("private void Logout_Click", StringComparison.Ordinal)..codeBehind.IndexOf("private void PasswordBox_PasswordChanged", StringComparison.Ordinal)];

        unlockHandler.ShouldContain("viewModel.SubmitUnlock");
        unlockHandler.IndexOf("viewModel.SubmitUnlock", StringComparison.Ordinal)
            .ShouldBeLessThan(unlockHandler.IndexOf("DialogResult = dialogResult.Value", StringComparison.Ordinal));
        logoutHandler.ShouldContain("viewModel.SubmitLogout");
        logoutHandler.IndexOf("viewModel.SubmitLogout", StringComparison.Ordinal)
            .ShouldBeLessThan(logoutHandler.IndexOf("DialogResult = dialogResult.Value", StringComparison.Ordinal));
        viewModel.ShouldContain("workflow.UnlockForLockScreen");
        viewModel.ShouldContain("workflow.Logout");
    }

    [Fact]
    public void Lock_window_renders_current_user_identity_and_localized_text()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "LockWindow.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LockWindow.xaml.cs");

        xaml.ShouldContain("LockedUserText");
        xaml.ShouldNotContain("Session locked");
        xaml.ShouldNotContain("Text=\"Password\"");
        xaml.ShouldNotContain("Content=\"Unlock\"");
        xaml.ShouldNotContain("Content=\"Logout\"");
        codeBehind.ShouldContain("ApplyLocalizedText()");
        codeBehind.ShouldContain("HostResourceKeys.LockTitle");
        codeBehind.ShouldContain("HostResourceKeys.LoginPassword");
        codeBehind.ShouldContain("HostResourceKeys.LockUnlock");
        codeBehind.ShouldContain("HostResourceKeys.LockLogout");
        xaml.ShouldContain("Text=\"{Binding CurrentUserLabel}\"");
        codeBehind.ShouldContain("DataContext = viewModel");
    }

    [Fact]
    public void Recovery_password_reset_dialog_reads_matching_password_fields_before_accepting()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "RecoveryPasswordResetDialog.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "RecoveryPasswordResetDialog.xaml.cs");

        xaml.ShouldContain("NewPasswordBox");
        xaml.ShouldContain("ConfirmPasswordBox");
        codeBehind.ShouldContain("NewPasswordBox.Password");
        codeBehind.ShouldContain("ConfirmPasswordBox.Password");
        codeBehind.IndexOf("SetAcceptedPassword", StringComparison.Ordinal)
            .ShouldBeLessThan(codeBehind.IndexOf("DialogResult = true", StringComparison.Ordinal));
    }

    [Fact]
    public void Login_failure_messages_use_login_specific_localization_keys()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        codeBehind.ShouldContain("HostResourceKeys.LoginInvalidCredentials");
        codeBehind.ShouldContain("HostResourceKeys.LoginDeactivated");
        codeBehind.ShouldContain("HostResourceKeys.LoginGenericFailure");
        codeBehind.ShouldNotContain("LoginAttemptKind.InvalidCredentials => strings.GetString(HostResourceKeys.RecoveryImportSignatureFailed)");
        codeBehind.ShouldNotContain("LoginAttemptKind.Deactivated => strings.GetString(HostResourceKeys.RecoveryImportOwnerMissing)");
    }

    [Fact]
    public void Recovery_view_visible_text_is_supplied_by_host_string_catalog()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "RecoveryView.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "RecoveryView.xaml.cs");

        foreach (var hardcoded in new[] { "Owner recovery", "Export request", "Open folder", "Copy path", "Import response" })
        {
            xaml.ShouldNotContain(hardcoded);
        }

        codeBehind.ShouldContain("ApplyLocalizedText()");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryTitle");
        codeBehind.ShouldContain("HostResourceKeys.LoginRecoveryExport");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryOpenFolder");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryCopyPath");
        codeBehind.ShouldContain("HostResourceKeys.LoginRecoveryImport");
    }

    [Fact]
    public void Recovery_password_reset_dialog_visible_text_is_supplied_by_host_string_catalog()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "RecoveryPasswordResetDialog.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "RecoveryPasswordResetDialog.xaml.cs");

        foreach (var hardcoded in new[] { "Title=\"Reset Owner Password\"", "Text=\"New password\"", "Text=\"Confirm password\"", "Content=\"Reset\"", "Content=\"Cancel\"" })
        {
            xaml.ShouldNotContain(hardcoded);
        }

        codeBehind.ShouldContain("ApplyLocalizedText()");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryResetTitle");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryResetNewPassword");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryResetConfirm");
        codeBehind.ShouldContain("HostResourceKeys.RecoveryResetCancel");
    }

    [Fact]
    public void Recovery_view_buttons_are_bound_to_view_model_actions()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "RecoveryView.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "RecoveryView.xaml.cs");

        xaml.ShouldContain("Click=\"Export_Click\"");
        xaml.ShouldContain("Click=\"Import_Click\"");
        xaml.ShouldContain("Click=\"OpenFolder_Click\"");
        xaml.ShouldContain("Click=\"CopyPath_Click\"");
        codeBehind.ShouldContain("viewModel.ExportRequest");
        codeBehind.ShouldContain("viewModel.ImportResponse");
        codeBehind.ShouldContain("viewModel.OpenExportFolder");
        codeBehind.ShouldContain("viewModel.CopyExportPath");
    }

    private static string ReadSource(params string[] relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot().FullName, Path.Combine(relativePath)));
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        current.ShouldNotBeNull();
        return current;
    }
}
