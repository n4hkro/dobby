using Dobby.Audit;
using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Audit;

public sealed class InMemoryAuditTrailTests
{
    [Fact]
    public void Snapshot_is_append_only_and_does_not_expose_mutable_storage()
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var first = CreateEvent(AuditEventName.UserLogin);
        var second = CreateEvent(AuditEventName.UserLogout);

        trail.Append(first);
        var snapshot = trail.Snapshot();
        trail.Append(second);

        snapshot.ShouldBe([first]);
        trail.Snapshot().ShouldBe([first, second]);
    }

    [Fact]
    public void Append_rejects_null_events()
    {
        IAuditTrail trail = new InMemoryAuditTrail();

        Should.Throw<ArgumentNullException>(() => trail.Append(null!));
    }

    private static AuditEvent CreateEvent(string name)
    {
        return new AuditEvent(name, "owner", "host", AuditResult.Success, CorrelationId.New(), DateTimeOffset.UtcNow);
    }
}
