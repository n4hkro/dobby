using System.Windows;
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
}
