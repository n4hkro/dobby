using System.Windows;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class WpfLockWindowPresenter : ILockWindowPresenter
{
    public bool ShowLockWindow(LockRuntimeWorkflow workflow, SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(session);

        var lockWindow = new LockWindow(workflow, session);
        var application = System.Windows.Application.Current;
        var owner = application?.Windows
            .OfType<Window>()
            .FirstOrDefault(candidate => candidate.IsActive && !ReferenceEquals(candidate, lockWindow))
            ?? application?.MainWindow;
        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, lockWindow))
        {
            lockWindow.Owner = owner;
        }

        var shielded = ShellSurfaceShield.Engage(
            application?.Windows.OfType<Window>().ToArray(),
            lockWindow);
        try
        {
            return lockWindow.ShowDialog() == true;
        }
        finally
        {
            ShellSurfaceShield.Release(shielded);
        }
    }
}
