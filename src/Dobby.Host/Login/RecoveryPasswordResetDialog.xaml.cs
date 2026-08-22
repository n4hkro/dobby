using System.Windows;
using System.Windows.Automation.Peers;
using Dobby.Localization;

namespace Dobby.Host.Login;

public partial class RecoveryPasswordResetDialog : Window
{
    private string? acceptedValue;
    private readonly HostStringCatalog strings = new();

    public RecoveryPasswordResetDialog()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public string? NewPassword
    {
        get
        {
            return acceptedValue;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewPasswordBox.Password) ||
            !StringComparer.Ordinal.Equals(NewPasswordBox.Password, ConfirmPasswordBox.Password))
        {
            ValidationText.Text = strings.GetString(HostResourceKeys.RecoveryResetValidation);
            ValidationText.Visibility = Visibility.Visible;
            RaiseLiveRegionChanged(ValidationText);
            (string.IsNullOrWhiteSpace(NewPasswordBox.Password)
                ? NewPasswordBox
                : ConfirmPasswordBox).Focus();
            return;
        }

        SetAcceptedPassword(NewPasswordBox.Password);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SetAcceptedPassword(string value)
    {
        acceptedValue = value;
    }

    private void ApplyLocalizedText()
    {
        Title = strings.GetString(HostResourceKeys.RecoveryResetTitle);
        NewPasswordLabel.Text = strings.GetString(HostResourceKeys.RecoveryResetNewPassword);
        ConfirmPasswordLabel.Text = strings.GetString(HostResourceKeys.RecoveryResetConfirm);
        ConfirmButton.Content = strings.GetString(HostResourceKeys.RecoveryResetConfirm);
        CancelButton.Content = strings.GetString(HostResourceKeys.RecoveryResetCancel);
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
}
