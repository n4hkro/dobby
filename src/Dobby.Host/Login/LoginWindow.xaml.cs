using System.Windows;
using System.Windows.Threading;
using Dobby.Core;
using Dobby.Localization;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public partial class LoginWindow : Window
{
    private readonly ILoginRuntimeWorkflow? loginWorkflow;
    private readonly LoginViewModel viewModel;
    private readonly RecoveryViewModel recoveryViewModel;
    private readonly HostStringCatalog strings = new();
    private DispatcherTimer? throttleTimer;
    private bool applyingInitialState;

    public LoginWindow()
        : this(null, new LoginViewModel(null), new RecoveryViewModel(
            new WindowsRecoveryFilePicker(),
            new WindowsRecoveryExportFolderOpener(),
            new WindowsRecoveryExportPathClipboard()))
    {
    }

    public LoginWindow(ILoginRuntimeWorkflow? loginWorkflow, LoginViewModel viewModel, RecoveryViewModel recoveryViewModel)
    {
        this.loginWorkflow = loginWorkflow;
        this.viewModel = viewModel;
        this.recoveryViewModel = recoveryViewModel;
        InitializeComponent();
        DataContext = viewModel;
        ApplyLocalizedText();
        ApplyInitialState();
        Closed += (_, _) => StopThrottleTimer();
    }

    public SessionRecord? AuthenticatedSession { get; private set; }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (loginWorkflow is null)
        {
            ShowError("Login runtime is unavailable.");
            return;
        }

        var result = loginWorkflow.Submit(new LoginSubmission(
            UsernameBox.Text,
            PasswordBox.Password,
            SubmittedDisplayName(),
            RememberMeCheckBox.IsChecked == true,
            CorrelationId.New()));
        viewModel.RefreshThrottleState();
        ApplyThrottleState();

        if (!result.IsAuthenticated)
        {
            ShowError(ToMessage(result));
            return;
        }

        AuthenticatedSession = result.Session;
        DialogResult = true;
    }

    private void Recovery_Click(object sender, RoutedEventArgs e)
    {
        var recoveryWindow = new Window
        {
            Title = strings.GetString(HostResourceKeys.RecoveryResetTitle),
            Owner = this,
            Width = 420,
            Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new RecoveryView(recoveryViewModel)
        };
        recoveryWindow.ShowDialog();
    }

    private void ApplyInitialState()
    {
        applyingInitialState = true;
        var displayNameVisibility = viewModel.ShouldShowDisplayNameInput
            ? Visibility.Visible
            : Visibility.Collapsed;
        DisplayNameLabel.Visibility = displayNameVisibility;
        DisplayNameBox.Visibility = displayNameVisibility;
        if (!viewModel.ShouldShowDisplayNameInput)
        {
            DisplayNameBox.Text = string.Empty;
        }

        UsernameBox.Text = viewModel.Username ?? string.Empty;
        if (viewModel.Password is not null)
        {
            PasswordBox.GetType().GetProperty(nameof(System.Windows.Controls.PasswordBox.Password))?.SetValue(PasswordBox, viewModel.Password);
        }

        applyingInitialState = false;
        ForgetSavedLoginButton.Visibility = viewModel.IsPasswordPrefilled ? Visibility.Visible : Visibility.Collapsed;
        viewModel.RefreshThrottleState();
        ApplyThrottleState();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (applyingInitialState)
        {
            return;
        }

        viewModel.EditPassword(PasswordBox.Password);
        ForgetSavedLoginButton.Visibility = viewModel.IsPasswordPrefilled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ForgetSavedLogin_Click(object sender, RoutedEventArgs e)
    {
        var result = viewModel.ForgetSavedLogin();
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorCode?.ToString() ?? ErrorCode.UnsupportedOperation.ToString());
            return;
        }

        UsernameBox.Text = string.Empty;
        PasswordBox.Clear();
        ForgetSavedLoginButton.Visibility = Visibility.Collapsed;
    }

    private void ApplyThrottleState()
    {
        LoginButton.IsEnabled = viewModel.IsLoginEnabled;
        if (viewModel.ThrottleCountdownText is null)
        {
            StopThrottleTimer();
            ThrottleCountdownText.Visibility = Visibility.Collapsed;
            ThrottleCountdownText.Text = string.Empty;
            return;
        }

        StartThrottleTimer();
        ThrottleCountdownText.Visibility = Visibility.Visible;
        ThrottleCountdownText.Text = $"{strings.GetString(HostResourceKeys.LoginThrottleCountdown)}: {viewModel.ThrottleCountdownText}";
    }

    private void StartThrottleTimer()
    {
        if (throttleTimer is not null)
        {
            return;
        }

        throttleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        throttleTimer.Tick += (_, _) =>
        {
            viewModel.RefreshThrottleState();
            ApplyThrottleState();
        };
        throttleTimer.Start();
    }

    private void StopThrottleTimer()
    {
        throttleTimer?.Stop();
        throttleTimer = null;
    }

    private void ApplyLocalizedText()
    {
        Title = strings.GetString(HostResourceKeys.LoginTitle);
        UsernameLabel.Text = strings.GetString(HostResourceKeys.LoginUsername);
        DisplayNameLabel.Text = strings.GetString(HostResourceKeys.LoginDisplayName);
        PasswordLabel.Text = strings.GetString(HostResourceKeys.LoginPassword);
        RememberMeCheckBox.Content = strings.GetString(HostResourceKeys.LoginRememberMe);
        ForgetSavedLoginButton.Content = strings.GetString(HostResourceKeys.LoginForgetSavedLogin);
        LoginButton.Content = strings.GetString(HostResourceKeys.LoginSubmit);
        RecoveryButton.Content = strings.GetString(HostResourceKeys.LoginRecoveryImport);
    }

    private string? SubmittedDisplayName()
    {
        if (!viewModel.ShouldShowDisplayNameInput)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(DisplayNameBox.Text) ? null : DisplayNameBox.Text;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private string ToMessage(LoginRuntimeWorkflowResult result)
    {
        if (result.AttemptKind == LoginAttemptKind.Throttled)
        {
            return $"{strings.GetString(HostResourceKeys.LoginThrottleCountdown)}: {result.RetryAfterSeconds}";
        }

        return result.AttemptKind switch
        {
            LoginAttemptKind.Deactivated => strings.GetString(HostResourceKeys.LoginDeactivated),
            LoginAttemptKind.InvalidCredentials => strings.GetString(HostResourceKeys.LoginInvalidCredentials),
            _ => strings.GetString(HostResourceKeys.LoginGenericFailure)
        };
    }
}
