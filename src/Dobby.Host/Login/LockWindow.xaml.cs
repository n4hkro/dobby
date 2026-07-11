using System.Windows;
using Dobby.Core;
using Dobby.Localization;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public partial class LockWindow : Window
{
    private readonly LockViewModel viewModel;
    private readonly HostStringCatalog strings = new();

    public LockWindow()
        : this(null, null)
    {
    }

    public LockWindow(LockRuntimeWorkflow? lockWorkflow, SessionRecord? session)
    {
        viewModel = CreateViewModel(lockWorkflow, session);
        InitializeComponent();
        DataContext = viewModel;
        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        Title = strings.GetString(HostResourceKeys.LockTitle);
        LockTitleText.Text = strings.GetString(HostResourceKeys.LockTitle);
        PasswordLabel.Text = strings.GetString(HostResourceKeys.LoginPassword);
        UnlockButton.Content = strings.GetString(HostResourceKeys.LockUnlock);
        LogoutButton.Content = strings.GetString(HostResourceKeys.LockLogout);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var dialogResult = viewModel.SubmitUnlock(CorrelationId.New());
        if (!dialogResult.HasValue)
        {
            return;
        }

        DialogResult = dialogResult.Value;
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var dialogResult = viewModel.SubmitLogout(CorrelationId.New());
        if (!dialogResult.HasValue)
        {
            return;
        }

        DialogResult = dialogResult.Value;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        viewModel.SubmittedPassword = PasswordBox.Password;
    }

    private LockViewModel CreateViewModel(LockRuntimeWorkflow? lockWorkflow, SessionRecord? session)
    {
        var userId = session?.UserId ?? string.Empty;
        if (lockWorkflow is null || string.IsNullOrWhiteSpace(userId))
        {
            return new LockViewModel("unknown", new NoOpLockViewModelActions(), strings);
        }

        return new LockViewModel(userId, new LockViewModelActions(lockWorkflow), strings);
    }

    private sealed class NoOpLockViewModelActions : ILockViewModelActions
    {
        public LockViewModelActionResult Unlock(string userId, string password, CorrelationId correlationId)
        {
            return LockViewModelActionResult.Failure(ErrorCode.NotFound);
        }

        public LockViewModelActionResult Logout(string userId, CorrelationId correlationId)
        {
            return LockViewModelActionResult.Failure(ErrorCode.NotFound);
        }
    }
}
