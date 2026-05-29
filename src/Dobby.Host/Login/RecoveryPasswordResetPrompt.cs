using System.Windows;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class RecoveryPasswordResetPrompt(Window? owner = null) : IRecoveryPasswordResetPrompt
{
    public string? CaptureNewOwnerPassword(RecoveryVerifiedChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var dialog = new RecoveryPasswordResetDialog();
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog.NewPassword : null;
    }
}
