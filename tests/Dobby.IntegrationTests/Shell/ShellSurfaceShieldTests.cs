using System.Windows;
using System.Windows.Controls;
using Dobby.Host.Login;
using Shouldly;

namespace Dobby.IntegrationTests.Shell;

/// <summary>
/// Runtime proof that locking a session conceals authenticated content instead of
/// leaving it readable behind the lock screen. Source inspection cannot close this
/// gate (`NFR-027`), so the assertions observe rendered visibility.
/// </summary>
[Collection(WpfIntegrationTestCollection.Name)]
public sealed class ShellSurfaceShieldTests
{
    [Fact]
    public void Shield_hides_authenticated_content_of_every_window_except_the_lock_screen()
    {
        RunOnSta(() =>
        {
            var shell = CreateWindow("shell business data");
            var secondary = CreateWindow("secondary authenticated surface");
            var lockScreen = CreateWindow("unlock prompt");

            try
            {
                var shieldedContent = (FrameworkElement)shell.Content;
                var secondaryContent = (FrameworkElement)secondary.Content;
                var lockContent = (FrameworkElement)lockScreen.Content;
                shieldedContent.IsVisible.ShouldBeTrue();

                var shielded = ShellSurfaceShield.Engage([shell, secondary, lockScreen], lockScreen);

                shielded.Count.ShouldBe(2);
                shieldedContent.IsVisible.ShouldBeFalse();
                secondaryContent.IsVisible.ShouldBeFalse();
                lockContent.IsVisible.ShouldBeTrue();
                shell.IsVisible.ShouldBeTrue();

                ShellSurfaceShield.Release(shielded);

                shieldedContent.IsVisible.ShouldBeTrue();
                secondaryContent.IsVisible.ShouldBeTrue();
            }
            finally
            {
                lockScreen.Close();
                secondary.Close();
                shell.Close();
            }
        });
    }

    [Fact]
    public void Shield_restores_the_previous_visibility_and_tolerates_absent_windows()
    {
        RunOnSta(() =>
        {
            var window = CreateWindow("collapsed authenticated surface");
            var content = (FrameworkElement)window.Content;
            content.Visibility = Visibility.Collapsed;

            try
            {
                var shielded = ShellSurfaceShield.Engage([window]);

                content.Visibility.ShouldBe(Visibility.Hidden);
                ShellSurfaceShield.Release(shielded);
                content.Visibility.ShouldBe(Visibility.Collapsed);

                ShellSurfaceShield.Engage(null).ShouldBeEmpty();
                ShellSurfaceShield.Release(null);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Window CreateWindow(string text)
    {
        var window = new Window
        {
            Content = new TextBlock { Text = text },
            Width = 240,
            Height = 160,
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
