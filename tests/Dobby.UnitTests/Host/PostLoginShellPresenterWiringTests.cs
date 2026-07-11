using System.IO;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class PostLoginShellPresenterWiringTests
{
    [Fact]
    public void Post_login_activation_refreshes_plugin_manager_surface_after_status_persistence()
    {
        var presenter = ReadSource("src", "Dobby.Host", "PostLoginShellPresenter.cs");

        var activation = presenter.IndexOf("BeginPostLoginActivation", StringComparison.Ordinal);
        var refresh = presenter.IndexOf("RefreshPluginsManagerSurfaceAsync", activation, StringComparison.Ordinal);
        var navigation = presenter.IndexOf("ApplyPluginNavigationContributions", activation, StringComparison.Ordinal);

        activation.ShouldBeGreaterThanOrEqualTo(0);
        refresh.ShouldBeGreaterThan(activation);
        navigation.ShouldBeGreaterThan(refresh);
    }

    private static string ReadSource(params string[] relativePath)
    {
        return File.ReadAllText(Path.Combine(RepoRoot().FullName, Path.Combine(relativePath)));
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        current.ShouldNotBeNull();
        return current;
    }
}
