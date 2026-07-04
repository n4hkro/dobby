using Dobby.Security.Authentication;
namespace Dobby.Host.Login;

public sealed class LoginViewModel
{
    public const string SavedPasswordPlaceholder = "********";

    private readonly LoginThrottleService? throttleService;
    private Func<string?>? passwordResolver;
    private string? passwordValue;

    public LoginViewModel(
        RememberMeSavedLogin? savedLogin,
        LoginThrottleService? throttleService = null,
        bool showDisplayNameInput = false)
    {
        this.throttleService = throttleService;
        ShouldShowDisplayNameInput = showDisplayNameInput;
        if (savedLogin is not null)
        {
            Username = savedLogin.MaskedUsername;
            if (savedLogin.HasMaskedPasswordPrefill)
            {
                passwordValue = savedLogin.Password;
                IsPasswordPrefilled = true;
                RememberMeSelected = true;
            }
        }
    }

    public static LoginViewModel CreateStartupPrefill(string username, Func<string?> resolvePassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(resolvePassword);

        return new LoginViewModel(null)
        {
            Username = username,
            passwordResolver = resolvePassword,
            IsPasswordPrefilled = true,
            RememberMeSelected = true
        };
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

    public string? InitialPasswordDisplayValue =>
        IsPasswordPrefilled && (passwordValue is not null || passwordResolver is not null)
            ? SavedPasswordPlaceholder
            : passwordValue;

    public bool HasStoredPasswordRevealControl => false;

    public bool RememberMeSelected { get; private set; }

    public bool ShouldShowDisplayNameInput { get; }

    public bool LoginWasAttempted { get; private set; }

    public bool IsLoginEnabled { get; private set; } = true;

    public string? ThrottleCountdownText { get; private set; }

    public void EditPassword(string password)
    {
        passwordValue = password;
        passwordResolver = null;
        IsPasswordPrefilled = false;
    }

    public string SubmittedPassword(string displayedPassword)
    {
        if (!IsPasswordPrefilled)
        {
            return displayedPassword;
        }

        if (passwordValue is not null)
        {
            return passwordValue;
        }

        var resolvedCredential = passwordResolver?.Invoke();
        if (resolvedCredential is not null)
        {
            passwordValue = resolvedCredential;
            passwordResolver = null;
            return resolvedCredential;
        }

        return displayedPassword;
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
