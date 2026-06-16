using System.IO;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LoginWindowRuntimeWiringTests
{
    [Fact]
    public void App_creates_login_runtime_and_passes_workflow_to_login_window()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");

        app.ShouldContain("HostLoginRuntimeFactory.Create()");
        app.ShouldContain("new LoginWindow(loginRuntime.LoginWorkflow, loginRuntime.LoginViewModel, loginRuntime.RecoveryViewModel)");
        app.ShouldContain("new MainWindow(");
        app.ShouldContain("loginRuntime.PostLoginSessionRuntime");
        app.ShouldContain("loginWindow.AuthenticatedSession");
        app.ShouldContain("loginRuntime.SelfServicePasswordChangeCoordinator");
    }

    [Fact]
    public void App_reopens_login_when_post_login_session_requests_login_return()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        app.ShouldContain("while (true)");
        app.ShouldContain("window.ShowDialog()");
        app.ShouldContain("window.ReturnToLoginRequested");
        mainWindow.ShouldContain("public bool ReturnToLoginRequested");
        mainWindow.ShouldContain("ReturnToLoginRequested = true");
        mainWindow.ShouldNotContain("result.ErrorCode != ErrorCode.Cancelled");
    }

    [Fact]
    public void App_and_host_login_runtime_use_explicit_async_cleanup()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var runtime = ReadSource("src", "Dobby.Host", "Login", "HostLoginRuntime.cs");

        app.ShouldContain("protected override async void OnStartup");
        app.ShouldContain("await RunLoginLoopAsync()");
        app.ShouldContain("await using var loginRuntime = HostLoginRuntimeFactory.Create()");
        runtime.ShouldContain("IAsyncDisposable");
        runtime.ShouldContain("PostLoginSessionRuntime.Dispose();");
        runtime.ShouldContain("await PluginRuntimeCoordinator.DisposeAsync()");
        runtime.IndexOf("PostLoginSessionRuntime.Dispose();", StringComparison.Ordinal)
            .ShouldBeLessThan(runtime.IndexOf("await PluginRuntimeCoordinator.DisposeAsync()", StringComparison.Ordinal));
    }

    [Fact]
    public void Main_window_records_host_input_as_session_activity()
    {
        var mainWindow = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        mainWindow.ShouldContain("PreviewKeyDown");
        mainWindow.ShouldContain("PreviewMouseDown");
        mainWindow.ShouldContain("RecordSessionActivity");
        mainWindow.ShouldContain("sessionRuntime.RecordActivity()");
    }

    [Fact]
    public void Main_window_exposes_account_password_change_and_manual_lock_surface()
    {
        var app = ReadSource("src", "Dobby.Host", "App.xaml.cs");
        var xaml = ReadSource("src", "Dobby.Host", "MainWindow.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "MainWindow.xaml.cs");

        app.ShouldContain("loginRuntime.SelfServicePasswordChangeCoordinator");
        xaml.ShouldContain("NewPasswordBox");
        xaml.ShouldContain("PasswordChangeRememberMeCheckBox");
        xaml.ShouldContain("ChangePasswordButton");
        xaml.ShouldContain("ManualLockButton");
        codeBehind.ShouldContain("SelfServicePasswordChangeCoordinator");
        codeBehind.ShouldContain("ChangePassword_Click");
        codeBehind.ShouldContain("passwordChangeCoordinator.ChangePassword");
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

        codeBehind.ShouldContain("loginWorkflow.Submit");
        codeBehind.IndexOf("loginWorkflow.Submit", StringComparison.Ordinal)
            .ShouldBeLessThan(codeBehind.IndexOf("DialogResult = true", StringComparison.Ordinal));
        codeBehind.ShouldNotContain("private void Continue_Click(object sender, RoutedEventArgs e)\r\n    {\r\n        DialogResult = true;\r\n    }");
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
    public void Login_window_wires_password_edit_and_explicit_forget_saved_login_actions()
    {
        var xaml = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml");
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");

        xaml.ShouldContain("PasswordChanged=\"PasswordBox_PasswordChanged\"");
        xaml.ShouldContain("ForgetSavedLoginButton");
        codeBehind.ShouldContain("viewModel.EditPassword(PasswordBox.Password)");
        codeBehind.ShouldContain("viewModel.ForgetSavedLogin()");
    }

    [Fact]
    public void Lock_window_submits_unlock_and_logout_to_runtime_workflow()
    {
        var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LockWindow.xaml.cs");

        codeBehind.ShouldContain("lockWorkflow.Unlock");
        codeBehind.IndexOf("lockWorkflow.Unlock", StringComparison.Ordinal)
            .ShouldBeLessThan(codeBehind.IndexOf("DialogResult = true", StringComparison.Ordinal));
        codeBehind.ShouldContain("lockWorkflow.Logout");
        codeBehind.IndexOf("lockWorkflow.Logout", StringComparison.Ordinal)
            .ShouldBeLessThan(codeBehind.IndexOf("DialogResult = false", StringComparison.Ordinal));
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
        codeBehind.ShouldContain("LockedUserText.Text = session?.UserId");
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
