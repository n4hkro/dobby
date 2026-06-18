using Microsoft.Win32;

namespace Dobby.Host.Plugins;

public interface ILicenseFilePicker
{
    string? PickLicenseFile();
}

public sealed class WindowsLicenseFilePicker : ILicenseFilePicker
{
    public string? PickLicenseFile()
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = "Dobby license files (*.dobbylicense)|*.dobbylicense|All files (*.*)|*.*",
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
