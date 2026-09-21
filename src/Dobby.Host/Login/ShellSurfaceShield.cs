using System.Windows;

namespace Dobby.Host.Login;

/// <summary>
/// Conceals authenticated shell content while the lock screen owns the foreground.
/// Locking a session must not leave business data, work session labels or device
/// identity readable on an unattended workstation, so the shield hides every
/// authenticated surface without tearing down process-lifetime window state.
/// </summary>
public sealed record ShieldedSurface(FrameworkElement Content, Visibility PreviousVisibility);

public static class ShellSurfaceShield
{
    public static IReadOnlyList<ShieldedSurface> Engage(
        IEnumerable<Window>? windows,
        Window? exclude = null)
    {
        if (windows is null)
        {
            return [];
        }

        var shielded = new List<ShieldedSurface>();
        foreach (var window in windows)
        {
            if (window is null || ReferenceEquals(window, exclude) || window.Content is not FrameworkElement content)
            {
                continue;
            }

            shielded.Add(new ShieldedSurface(content, content.Visibility));
            content.Visibility = Visibility.Hidden;
        }

        return shielded;
    }

    public static void Release(IReadOnlyList<ShieldedSurface>? shielded)
    {
        foreach (var surface in shielded ?? [])
        {
            surface.Content.Visibility = surface.PreviousVisibility;
        }
    }
}
