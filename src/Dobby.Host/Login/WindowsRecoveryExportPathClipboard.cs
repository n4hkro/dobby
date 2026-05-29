using System.Windows;

namespace Dobby.Host.Login;

public sealed class WindowsRecoveryExportPathClipboard : IRecoveryExportPathClipboard
{
    public void CopyPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Clipboard.SetText(path);
    }
}
