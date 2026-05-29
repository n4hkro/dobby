using Microsoft.Win32;

namespace Dobby.Host.Login;

public sealed class WindowsRecoveryFilePicker : IRecoveryFilePicker
{
    public string? PickRecoveryResponseFile()
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Dobby recovery responses (*.dobbyrecovery)|*.dobbyrecovery|All files (*.*)|*.*",
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
