using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class LoginViewModel
{
    private readonly LoginThrottleService? throttleService;
    private readonly string? throttleAccountKey;
    private string? passwordValue;

    public LoginViewModel(
        RememberedLoginAccount? rememberedAccount,
        LoginThrottleService? throttleService = null,
        bool showDisplayNameInput = false)
    {
        this.throttleService = throttleService;
        ShouldShowDisplayNameInput = showDisplayNameInput;
        if (rememberedAccount is not null)
        {
            Username = rememberedAccount.UsernameNormalized;
            RememberMeSelected = true;
            throttleAccountKey = rememberedAccount.UserId;
        }
    }

    public string? Username { get; private set; }

    public string? Password => passwordValue;

    public bool RememberMeSelected { get; private set; }

    public bool ShouldShowDisplayNameInput { get; }

    public bool LoginWasAttempted { get; private set; }

    public bool IsLoginEnabled { get; private set; } = true;

    public string? ThrottleCountdownText { get; private set; }

    public void EditPassword(string password)
    {
        passwordValue = password;
    }

    public void RefreshThrottleState()
    {
        var state = throttleService?.Check(throttleAccountKey);
        IsLoginEnabled = state is null || !state.IsThrottled;
        ThrottleCountdownText = state is { IsThrottled: true }
            ? state.RetryAfterSeconds.ToString()
            : null;
    }
}
