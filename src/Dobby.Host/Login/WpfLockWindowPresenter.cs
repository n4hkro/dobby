using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class WpfLockWindowPresenter : ILockWindowPresenter
{
    public bool ShowLockWindow(LockRuntimeWorkflow workflow, SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(session);

        return new LockWindow(workflow, session).ShowDialog() == true;
    }
}
