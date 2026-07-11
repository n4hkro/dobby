using System.IO;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Localization;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LockViewModelTests
{
    [Fact]
    public void Source_defines_lock_view_model_and_keeps_codebehind_as_wpf_adapter_only()
    {
        var viewModelSource = ReadSource("src", "Dobby.Host", "Login", "LockViewModel.cs");
        var xamlSource = ReadSource("src", "Dobby.Host", "Login", "LockWindow.xaml");
        var codeBehindSource = ReadSource("src", "Dobby.Host", "Login", "LockWindow.xaml.cs");

        viewModelSource.ShouldContain("public sealed class LockViewModel");
        viewModelSource.ShouldContain("public string CurrentUserLabel");
        viewModelSource.ShouldContain("public string SubmittedPassword");
        viewModelSource.ShouldContain("public bool? SubmitUnlock");
        viewModelSource.ShouldContain("public bool? SubmitLogout");
        viewModelSource.ShouldContain("public bool IsBusy");
        viewModelSource.ShouldContain("public LockViewModelFeedbackKind FeedbackKind");
        viewModelSource.ShouldContain("public int RetryAfterSeconds");
        viewModelSource.ShouldContain("public string FeedbackText");
        xamlSource.ShouldContain("Text=\"{Binding CurrentUserLabel}\"");
        xamlSource.ShouldContain("Text=\"{Binding FeedbackText}\"");
        xamlSource.ShouldContain("IsEnabled=\"{Binding UnlockEnabled}\"");
        xamlSource.ShouldContain("IsEnabled=\"{Binding LogoutEnabled}\"");
        codeBehindSource.ShouldContain("DataContext = viewModel");
        codeBehindSource.ShouldContain("viewModel.SubmittedPassword = PasswordBox.Password");
        codeBehindSource.ShouldContain("var dialogResult = viewModel.SubmitUnlock");
        codeBehindSource.ShouldContain("var dialogResult = viewModel.SubmitLogout");
        viewModelSource.ShouldContain("workflow.UnlockForLockScreen");
        codeBehindSource.ShouldNotContain("lockWorkflow.Unlock");
        codeBehindSource.ShouldNotContain("lockWorkflow.Logout");
        codeBehindSource.ShouldNotContain("LockedUserText.Text = session?.UserId");
    }

    [Fact]
    public void Unlock_failure_surfaces_generic_error_without_raw_credential_detail()
    {
        var viewModel = new LockViewModel(
            "owner-1",
            new StubLockActions(
                unlockResult: LockViewModelActionResult.Failure(ErrorCode.Unauthorized),
                logoutResult: new LockViewModelActionResult(IsSuccess: true)),
            new HostStringCatalog());
        viewModel.SubmittedPassword = "wrong";

        var dialogResult = viewModel.SubmitUnlock(CorrelationId.New());

        dialogResult.ShouldBeNull();
        viewModel.FeedbackKind.ShouldBe(LockViewModelFeedbackKind.InvalidCredentials);
        viewModel.RetryAfterSeconds.ShouldBe(0);
        viewModel.CountdownText.ShouldBeNull();
        viewModel.FeedbackText.ShouldBe(new HostStringCatalog().GetString(HostResourceKeys.LoginInvalidCredentials));
        viewModel.FeedbackText.ShouldNotContain("wrong");
        viewModel.IsBusy.ShouldBeFalse();
        viewModel.UnlockEnabled.ShouldBeTrue();
        viewModel.LogoutEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Unlock_and_logout_delegate_to_lock_actions_and_toggle_busy_state()
    {
        var actions = new StubLockActions(
            new LockViewModelActionResult(IsSuccess: true),
            new LockViewModelActionResult(IsSuccess: true));
        var viewModel = new LockViewModel("owner-1", actions, new HostStringCatalog())
        {
            SubmittedPassword = "correct"
        };

        var unlockResult = viewModel.SubmitUnlock(CorrelationId.New());
        var logoutResult = viewModel.SubmitLogout(CorrelationId.New());

        unlockResult.ShouldBe(true);
        logoutResult.ShouldBe(false);
        actions.UnlockCalls.ShouldBe(1);
        actions.LogoutCalls.ShouldBe(1);
        actions.LastUnlockUserId.ShouldBe("owner-1");
        actions.LastUnlockPassword.ShouldBe("correct");
        actions.LastLogoutUserId.ShouldBe("owner-1");
        viewModel.IsBusy.ShouldBeFalse();
        viewModel.FeedbackKind.ShouldBe(LockViewModelFeedbackKind.None);
        viewModel.FeedbackText.ShouldBeEmpty();
    }

    [Fact]
    public void Countdown_failure_exposes_typed_retry_state()
    {
        var viewModel = new LockViewModel(
            "owner-1",
            new StubLockActions(
                LockViewModelActionResult.Failure(ErrorCode.Unauthorized, retryAfterSeconds: 10),
                new LockViewModelActionResult(IsSuccess: true)),
            new HostStringCatalog());

        var dialogResult = viewModel.SubmitUnlock(CorrelationId.New());

        dialogResult.ShouldBeNull();
        viewModel.FeedbackKind.ShouldBe(LockViewModelFeedbackKind.Countdown);
        viewModel.RetryAfterSeconds.ShouldBe(10);
        viewModel.CountdownText.ShouldBe("10");
        viewModel.FeedbackText.ShouldContain("10");
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

    private sealed class StubLockActions(LockViewModelActionResult unlockResult, LockViewModelActionResult logoutResult) : ILockViewModelActions
    {
        public int UnlockCalls { get; private set; }

        public int LogoutCalls { get; private set; }

        public string? LastUnlockUserId { get; private set; }

        public string? LastUnlockPassword { get; private set; }

        public string? LastLogoutUserId { get; private set; }

        public LockViewModelActionResult Unlock(string userId, string password, CorrelationId correlationId)
        {
            UnlockCalls++;
            LastUnlockUserId = userId;
            LastUnlockPassword = password;
            return unlockResult;
        }

        public LockViewModelActionResult Logout(string userId, CorrelationId correlationId)
        {
            LogoutCalls++;
            LastLogoutUserId = userId;
            return logoutResult;
        }
    }
}
