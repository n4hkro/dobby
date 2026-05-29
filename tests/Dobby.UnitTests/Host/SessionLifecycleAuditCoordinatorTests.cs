using Dobby.Audit;
using Dobby.Core;
using Dobby.Host.Sessions;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class SessionLifecycleAuditCoordinatorTests
{
    [Fact]
    public void Lock_transition_checkpoint_appends_single_session_lock_audit()
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new SessionLifecycleAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));
        var correlationId = CorrelationId.New();

        coordinator.AppendCheckpoint("user-1", new SessionCheckpoint(SessionCheckpointKind.Locked, JustTransitioned: true), correlationId);
        coordinator.AppendCheckpoint("user-1", new SessionCheckpoint(SessionCheckpointKind.Locked, JustTransitioned: false), correlationId);

        var audit = auditTrail.Snapshot().Single();
        audit.EventName.ShouldBe(AuditEventName.SessionLock);
        audit.Result.ShouldBe(AuditResult.Success);
        audit.ActorId.ShouldBe("user-1");
        audit.Target.ShouldBe("host-session");
    }

    [Theory]
    [InlineData("Unauthorized", "wrong-password")]
    [InlineData("NotFound", "no-session")]
    [InlineData("DatabaseUnavailable", "credential-unavailable")]
    public void Failed_unlock_appends_session_unlock_failed_with_required_reason(string errorCode, string reason)
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new SessionLifecycleAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));

        coordinator.AppendUnlock("user-1", Result.Failure(new ErrorCode(errorCode)), CorrelationId.New());

        var audit = auditTrail.Snapshot().Single();
        audit.EventName.ShouldBe(AuditEventName.SessionUnlock);
        audit.Result.ShouldBe(AuditResult.Failed);
        audit.ActorId.ShouldBe("user-1");
        audit.Target.ShouldBe("host-session");
        audit.Details.ShouldNotBeNull();
        audit.Details["reason"].ShouldBe(reason);
    }

    [Fact]
    public void Successful_unlock_appends_session_unlock_success()
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new SessionLifecycleAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));

        coordinator.AppendUnlock("user-1", Result.Success(), CorrelationId.New());

        var audit = auditTrail.Snapshot().Single();
        audit.EventName.ShouldBe(AuditEventName.SessionUnlock);
        audit.Result.ShouldBe(AuditResult.Success);
        audit.ActorId.ShouldBe("user-1");
        audit.Target.ShouldBe("host-session");
        audit.Details.ShouldBeNull();
    }

    [Fact]
    public void Logout_and_expired_checkpoint_append_user_logout_and_session_expired()
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new SessionLifecycleAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));
        var correlationId = CorrelationId.New();

        coordinator.AppendLogout("user-1", Result.Success(), correlationId);
        coordinator.AppendCheckpoint("user-1", new SessionCheckpoint(SessionCheckpointKind.Expired, "absolute"), correlationId);

        var events = auditTrail.Snapshot();
        events.Select(audit => audit.EventName).ShouldBe([AuditEventName.UserLogout, AuditEventName.SessionExpired]);
        events[0].Result.ShouldBe(AuditResult.Success);
        events[1].Result.ShouldBe(AuditResult.Success);
        events[1].Details.ShouldNotBeNull();
        events[1].Details!["reason"].ShouldBe("absolute");
    }

    [Fact]
    public void Invalidated_checkpoint_appends_session_invalidated_audit()
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new SessionLifecycleAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));

        coordinator.AppendCheckpoint("user-1", new SessionCheckpoint(SessionCheckpointKind.Invalidated, "deactivated"), CorrelationId.New());

        var audit = auditTrail.Snapshot().Single();
        audit.EventName.ShouldBe(AuditEventName.SessionInvalidated);
        audit.Result.ShouldBe(AuditResult.Success);
        audit.ActorId.ShouldBe("user-1");
        audit.Target.ShouldBe("host-session");
        audit.Details.ShouldNotBeNull();
        audit.Details["reason"].ShouldBe("deactivated");
    }
}
