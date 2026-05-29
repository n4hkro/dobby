using Shouldly;
using System.IO;

namespace Dobby.IntegrationTests.Startup;

public sealed class HostRuntimeSurfaceTests
{
    [Fact]
    public void Host_contains_minimal_login_lock_and_recovery_wpf_surfaces()
    {
        var host = Path.Combine(RepoRoot().FullName, "src", "Dobby.Host");

        File.Exists(Path.Combine(host, "Login", "LoginWindow.xaml")).ShouldBeTrue();
        File.Exists(Path.Combine(host, "Login", "LoginWindow.xaml.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(host, "Login", "LockWindow.xaml")).ShouldBeTrue();
        File.Exists(Path.Combine(host, "Login", "LockWindow.xaml.cs")).ShouldBeTrue();
        File.Exists(Path.Combine(host, "Login", "RecoveryView.xaml")).ShouldBeTrue();
        File.Exists(Path.Combine(host, "Login", "RecoveryPasswordResetDialog.xaml")).ShouldBeTrue();
    }

    [Fact]
    public void App_startup_uses_login_first_flow_before_post_login_main_window()
    {
        var app = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "App.xaml.cs"));

        var singleInstanceIndex = app.IndexOf("new SingleInstanceCoordinator", StringComparison.Ordinal);
        var bootstrapIndex = app.IndexOf("HostBootstrapper.Bootstrap", StringComparison.Ordinal);
        var loginWindowIndex = app.IndexOf("new LoginWindow", StringComparison.Ordinal);
        var mainWindowIndex = app.IndexOf("new MainWindow", StringComparison.Ordinal);

        singleInstanceIndex.ShouldBeGreaterThanOrEqualTo(0);
        bootstrapIndex.ShouldBeGreaterThan(singleInstanceIndex);
        loginWindowIndex.ShouldBeGreaterThan(bootstrapIndex);
        mainWindowIndex.ShouldBeGreaterThan(loginWindowIndex);
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
