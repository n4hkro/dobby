using Dobby.Shell.WorkSessions;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class WorkSessionRegistryTests
{
    [Fact]
    public void Work_sessions_are_process_lifetime_and_explicitly_minimized()
    {
        var registry = new WorkSessionRegistry();

        var session = registry.Open("sample", "Dobby.Shell.Navigation.Home", "Dobby.Shell.Home.Title");
        registry.Minimize(session.SessionId);
        registry.Restore(session.SessionId);

        var restored = registry.Get(session.SessionId);
        restored.ShouldNotBeNull();
        restored.IsMinimized.ShouldBeFalse();
        registry.Snapshot().Single().RouteKey.ShouldBe("Dobby.Shell.Navigation.Home");
    }
}
