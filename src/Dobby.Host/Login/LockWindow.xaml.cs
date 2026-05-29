using System.Windows;
using Dobby.Core;
using Dobby.Localization;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public partial class LockWindow : Window
{
    private readonly LockRuntimeWorkflow? lockWorkflow;
    private readonly SessionRecord? session;
    private readonly HostStringCatalog strings = new();

    public LockWindow()
        : this(null, null)
    {
    }

    public LockWindow(LockRuntimeWorkflow? lockWorkflow, SessionRecord? session)
    {
        this.lockWorkflow = lockWorkflow;
        this.session = session;
        InitializeComponent();
        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        Title = strings.GetString(HostResourceKeys.LockTitle);
        LockTitleText.Text = strings.GetString(HostResourceKeys.LockTitle);
        LockedUserText.Text = session?.UserId ?? string.Empty;
        PasswordLabel.Text = strings.GetString(HostResourceKeys.LoginPassword);
        UnlockButton.Content = strings.GetString(HostResourceKeys.LockUnlock);
        LogoutButton.Content = strings.GetString(HostResourceKeys.LockLogout);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (lockWorkflow is null || session is null)
        {
            return;
        }

        var result = lockWorkflow.Unlock(session.UserId, PasswordBox.Password, CorrelationId.New());
        if (!result.IsSuccess)
        {
            return;
        }

        DialogResult = true;
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (lockWorkflow is not null && session is not null)
        {
            var result = lockWorkflow.Logout(session.UserId, CorrelationId.New());
            if (!result.IsSuccess)
            {
                return;
            }
        }

        DialogResult = false;
    }
}
