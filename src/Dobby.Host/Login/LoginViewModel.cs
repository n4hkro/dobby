using Dobby.Security.Authentication;
using Dobby.Core;

namespace Dobby.Host.Login;

public interface ISavedLoginClearer
{
    Result ForgetSavedLogin();
}

public sealed class LoginViewModel
{
    private readonly LoginThrottleService? throttleService;
    private readonly ISavedLoginClearer? savedLoginClearer;
    private string? passwordValue;

    public LoginViewModel(
        RememberMeSavedLogin? savedLogin,
        LoginThrottleService? throttleService = null,
        ISavedLoginClearer? savedLoginClearer = null,
        bool showDisplayNameInput = false)
    {
        this.throttleService = throttleService;
        this.savedLoginClearer = savedLoginClearer;
        ShouldShowDisplayNameInput = showDisplayNameInput;
        if (savedLogin is not null)
        {
            Username = savedLogin.MaskedUsername;
            if (savedLogin.HasMaskedPasswordPrefill)
            {
                passwordValue = "••••••••";
                IsPasswordPrefilled = true;
            }
        }
    }

    public string? Username { get; private set; }

    public string? Password
    {
        get
        {
            return passwordValue;
        }
    }

    public bool IsPasswordPrefilled { get; private set; }

    public bool HasStoredPasswordRevealControl => false;

    public bool ShouldShowDisplayNameInput { get; }

    public bool LoginWasAttempted { get; private set; }

    public bool ForgetSavedLoginRequested { get; private set; }

    public bool IsLoginEnabled { get; private set; } = true;

    public string? ThrottleCountdownText { get; private set; }

    public void EditPassword(string password)
    {
        passwordValue = password;
        IsPasswordPrefilled = false;
    }

    public Result ForgetSavedLogin()
    {
        var result = savedLoginClearer?.ForgetSavedLogin() ?? Result.Failure(ErrorCode.UnsupportedOperation);
        if (!result.IsSuccess)
        {
            return result;
        }

        Username = null;
        passwordValue = null;
        IsPasswordPrefilled = false;
        ForgetSavedLoginRequested = true;
        return Result.Success();
    }

    public void RefreshThrottleState()
    {
        var state = throttleService?.Check();
        IsLoginEnabled = state is null || !state.IsThrottled;
        ThrottleCountdownText = state is { IsThrottled: true }
            ? state.RetryAfterSeconds.ToString()
            : null;
    }
}
