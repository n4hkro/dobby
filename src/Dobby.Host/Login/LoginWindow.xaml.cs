using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using Dobby.Core;
using Dobby.Localization;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public partial class LoginWindow : Window
{
    private ILoginRuntimeWorkflow? loginWorkflow;
    private LoginViewModel viewModel;
    private RecoveryViewModel? recoveryViewModel;
    private readonly HostStringCatalog strings = new();
    private DispatcherTimer? throttleTimer;
    private bool applyingInitialState;
    private bool userEditedLoginFields;

    public LoginWindow()
        : this(LoginStartupSnapshot.Default)
    {
    }

    public LoginWindow(LoginStartupSnapshot startupSnapshot)
        : this(null, startupSnapshot.CreateLoginViewModel(), recoveryViewModel: null)
    {
    }

    public LoginWindow(ILoginRuntimeWorkflow? loginWorkflow, LoginViewModel viewModel, RecoveryViewModel? recoveryViewModel)
    {
        this.loginWorkflow = loginWorkflow;
        this.viewModel = viewModel;
        this.recoveryViewModel = recoveryViewModel;
        InitializeComponent();
        ApplyLocalizedText();
        ApplyInitialState();
        Closed += (_, _) =>
        {
            StopThrottleTimer();
        };
    }

    public SessionRecord? AuthenticatedSession { get; private set; }

    internal PostLoginTimingTrace? PostLoginTiming { get; private set; }

    public void AttachDeferredWorkflow(ILoginRuntimeWorkflow runtimeWorkflow)
    {
        ArgumentNullException.ThrowIfNull(runtimeWorkflow);

        loginWorkflow = runtimeWorkflow;
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
        ApplyThrottleState();
    }

    public void AttachRuntime(HostPreLoginRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        AttachRuntime(runtime.LoginWorkflow, runtime.LoginViewModel, runtime.RecoveryViewModel);
    }

    public void AttachRuntime(HostLoginRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        AttachRuntime(runtime.LoginWorkflow, runtime.LoginViewModel, runtime.RecoveryViewModel);
    }

    private void AttachRuntime(
        ILoginRuntimeWorkflow runtimeWorkflow,
        LoginViewModel runtimeViewModel,
        RecoveryViewModel runtimeRecoveryViewModel)
    {
        var shouldKeepRenderedLoginFields = ShouldKeepRenderedLoginFields(runtimeViewModel);
        loginWorkflow = runtimeWorkflow;
        viewModel = runtimeViewModel;
        recoveryViewModel = runtimeRecoveryViewModel;
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
        if (userEditedLoginFields)
        {
            ApplyInitialState(preserveLoginFields: true);
            return;
        }

        if (shouldKeepRenderedLoginFields)
        {
            ApplyRuntimeStateWithoutLoginFieldReset();
            return;
        }

        ApplyInitialState();
    }

    public void ShowRuntimeInitializationFailure()
    {
        ShowError(strings.GetString(HostResourceKeys.LoginGenericFailure));
        ApplyThrottleState();
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        var workflow = loginWorkflow;
        if (workflow is null)
        {
            ShowRuntimeInitializationFailure();
            return;
        }

        var postLoginTiming = PostLoginTimingTrace.Start();
        PostLoginTiming = postLoginTiming;
        var submission = new LoginSubmission(
            UsernameBox.Text,
            PasswordBox.Password,
            SubmittedDisplayName(),
            RememberMeCheckBox.IsChecked == true,
            CorrelationId.New());
        LoginButton.IsEnabled = false;
        RecoveryButton.IsEnabled = false;

        LoginRuntimeWorkflowResult result;
        try
        {
            result = await Task.Run(() => workflow.Submit(submission));
            postLoginTiming.MarkLoginSubmitCompleted();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            ShowRuntimeInitializationFailure();
            return;
        }
        finally
        {
            viewModel.RefreshThrottleState();
            ApplyThrottleState();
        }

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
        if (loginWorkflow is null || recoveryViewModel is null)
        {
            ShowRuntimeInitializationFailure();
            return;
        }

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

    private void ApplyInitialState(bool preserveLoginFields = false)
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

        if (!preserveLoginFields)
        {
            UsernameBox.Text = viewModel.Username ?? string.Empty;
            RememberMeCheckBox.IsChecked = viewModel.RememberMeSelected;
        }
        else
        {
            viewModel.EditPassword(PasswordBox.Password);
        }

        applyingInitialState = false;
        viewModel.RefreshThrottleState();
        ApplyThrottleState();
    }

    private bool ShouldKeepRenderedLoginFields(LoginViewModel runtimeViewModel)
    {
        return viewModel.ShouldShowDisplayNameInput == runtimeViewModel.ShouldShowDisplayNameInput &&
            viewModel.RememberMeSelected == runtimeViewModel.RememberMeSelected &&
            StringComparer.Ordinal.Equals(viewModel.Username, runtimeViewModel.Username);
    }

    private void ApplyRuntimeStateWithoutLoginFieldReset()
    {
        viewModel.RefreshThrottleState();
        ApplyThrottleState();
    }

    private void UsernameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!applyingInitialState)
        {
            userEditedLoginFields = true;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (applyingInitialState)
        {
            return;
        }

        userEditedLoginFields = true;
        viewModel.EditPassword(PasswordBox.Password);
    }

    private void ApplyThrottleState()
    {
        LoginButton.IsEnabled = loginWorkflow is not null && viewModel.IsLoginEnabled;
        RecoveryButton.IsEnabled = loginWorkflow is not null;
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
        RaiseLiveRegionChanged(ThrottleCountdownText);
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
        RaiseLiveRegionChanged(ErrorText);
    }

    private static void RaiseLiveRegionChanged(UIElement element)
    {
        var peer = UIElementAutomationPeer.FromElement(element) ??
            UIElementAutomationPeer.CreatePeerForElement(element);
        if (peer is not null && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
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
