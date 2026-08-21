using Dobby.Configuration;
namespace Dobby.Host.Login;

public sealed record LoginStartupSnapshot(AppThemePreference Theme)
{
    public static LoginStartupSnapshot Default { get; } = new(AppThemePreference.Light);

    public LoginViewModel CreateLoginViewModel()
    {
        return new LoginViewModel(null);
    }
}
