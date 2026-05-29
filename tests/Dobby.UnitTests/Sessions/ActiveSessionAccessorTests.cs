using Dobby.Core;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Sessions;

public sealed class ActiveSessionAccessorTests
{
    [Fact]
    public void Set_active_session_enforces_single_active_session_until_cleared()
    {
        var accessor = new ActiveSessionAccessor();
        var first = new SessionRecord("session-1", "user-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), null, 1);
        var second = first with { SessionId = "session-2" };

        accessor.SetActive(first).IsSuccess.ShouldBeTrue();
        var rejected = accessor.SetActive(second);

        rejected.IsSuccess.ShouldBeFalse();
        rejected.ErrorCode.ShouldBe(ErrorCode.AlreadyExists);
        accessor.Current.ShouldBe(first);
        accessor.Clear();
        accessor.SetActive(second).IsSuccess.ShouldBeTrue();
    }
}
