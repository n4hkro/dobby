using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Dobby.Host;

internal static class WindowActivationHelper
{
    private const int ShowWindowCommandShow = 5;

    public static void Activate(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!window.IsVisible)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, ShowWindowCommandShow);
            SetForegroundWindow(handle);
        }

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
