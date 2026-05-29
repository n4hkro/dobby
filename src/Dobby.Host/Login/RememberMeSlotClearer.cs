using Dobby.Core;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class RememberMeSlotClearer(RememberMeSlotService rememberMeSlotService) : ISavedLoginClearer
{
    public Result ForgetSavedLogin()
    {
        return rememberMeSlotService.ForgetSavedLogin();
    }
}
