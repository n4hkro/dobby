using System.Windows;
using Dobby.Localization;

namespace Dobby.Host;

public interface IShellWindowCloseConfirmation
{
    bool ConfirmClose();
}

public sealed class WpfShellWindowCloseConfirmation(
    Window owner,
    HostStringCatalog? strings = null) : IShellWindowCloseConfirmation
{
    private readonly HostStringCatalog stringCatalog = strings ?? HostStringCatalog.CreateResourceBacked();

    public bool ConfirmClose()
    {
        var response = MessageBox.Show(
            owner,
            stringCatalog.GetString(HostResourceKeys.ShellWindowCloseConfirmationMessage),
            stringCatalog.GetString(HostResourceKeys.ShellWindowCloseConfirmationTitle),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);

        return response == MessageBoxResult.OK;
    }
}
