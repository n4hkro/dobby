using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Host.Sessions;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class PostLoginSessionRuntimeTests
{
    [Fact]
    public void Auto_lock_checkpoint_requests_lock_window_and_records_lock_audit_without_invalidating_session()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var credentials = Substitute.For<IUserCredentialVerifier>();
        var sessionService = new SessionLifecycleService(
            store,
            accessor,
            credentials,
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = true });
        var session = sessionService.Create("user-1").Value;
        var auditTrail = new InMemoryAuditTrail();
        var audit = new SessionLifecycleAuditCoordinator(auditTrail, time);
        var presenter = new RecordingLockWindowPresenter(unlockResult: true);
        var runtime = new PostLoginSessionRuntime(
            sessionService,
            audit,
            new LockRuntimeWorkflow(sessionService, audit),
            presenter);

        time.Advance(TimeSpan.FromMinutes(30));
        var result = runtime.Checkpoint(session, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        presenter.PresentedSession.ShouldBe(session);
        auditTrail.Snapshot().Single().EventName.ShouldBe(AuditEventName.SessionLock);
        store.DidNotReceiveWithAnyArgs().Invalidate(default!, default!, default);
    }

    [Fact]
    public void Expired_checkpoint_returns_to_login_without_showing_lock_window()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var sessionService = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = false });
        var session = sessionService.Create("user-1").Value;
        var auditTrail = new InMemoryAuditTrail();
        var audit = new SessionLifecycleAuditCoordinator(auditTrail, time);
        var presenter = new RecordingLockWindowPresenter(unlockResult: true);
        var runtime = new PostLoginSessionRuntime(
            sessionService,
            audit,
            new LockRuntimeWorkflow(sessionService, audit),
            presenter);

        time.Advance(TimeSpan.FromMinutes(30));
        var result = runtime.Checkpoint(session, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
        presenter.PresentedSession.ShouldBeNull();
        auditTrail.Snapshot().Single().EventName.ShouldBe(AuditEventName.SessionExpired);
    }

    [Fact]
    public void Manual_lock_uses_same_lock_window_surface_and_preserves_session_row()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var sessionService = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var session = sessionService.Create("user-1").Value;
        var auditTrail = new InMemoryAuditTrail();
        var audit = new SessionLifecycleAuditCoordinator(auditTrail, time);
        var presenter = new RecordingLockWindowPresenter(unlockResult: true);
        var runtime = new PostLoginSessionRuntime(
            sessionService,
            audit,
            new LockRuntimeWorkflow(sessionService, audit),
            presenter);

        var result = runtime.LockNow(session, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        presenter.PresentedSession.ShouldBe(session);
        auditTrail.Snapshot().Single().EventName.ShouldBe(AuditEventName.SessionLock);
        store.DidNotReceiveWithAnyArgs().Invalidate(default!, default!, default);
    }

    [Fact]
    public void Manual_lock_logout_result_returns_to_login()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var sessionService = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var session = sessionService.Create("user-1").Value;
        var audit = new SessionLifecycleAuditCoordinator(new InMemoryAuditTrail(), time);
        var runtime = new PostLoginSessionRuntime(
            sessionService,
            audit,
            new LockRuntimeWorkflow(sessionService, audit),
            new RecordingLockWindowPresenter(unlockResult: false));

        var result = runtime.LockNow(session, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Cancelled);
    }

    [Fact]
    public void Recorded_activity_through_post_login_runtime_extends_idle_window()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var sessionService = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = false });
        var session = sessionService.Create("user-1").Value;
        var runtime = new PostLoginSessionRuntime(
            sessionService,
            new SessionLifecycleAuditCoordinator(new InMemoryAuditTrail(), time),
            new LockRuntimeWorkflow(sessionService, new SessionLifecycleAuditCoordinator(new InMemoryAuditTrail(), time)),
            new RecordingLockWindowPresenter(unlockResult: true));

        time.Advance(TimeSpan.FromMinutes(20));
        runtime.RecordActivity().IsSuccess.ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(20));
        var result = runtime.Checkpoint(session, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        store.Received(1).UpdateLastActivity(session.SessionId, time.GetUtcNow().AddMinutes(-20));
        store.DidNotReceive().Invalidate(session.SessionId, "idle", Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void Logout_invalidates_current_session_and_records_logout_audit()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var sessionService = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var session = sessionService.Create("user-1").Value;
        var auditTrail = new InMemoryAuditTrail();
        var audit = new SessionLifecycleAuditCoordinator(auditTrail, time);
        var runtime = new PostLoginSessionRuntime(
            sessionService,
            audit,
            new LockRuntimeWorkflow(sessionService, audit),
            new RecordingLockWindowPresenter(unlockResult: true));
        var correlationId = CorrelationId.New();

        var result = runtime.Logout(session, correlationId);

        result.IsSuccess.ShouldBeTrue();
        store.Received(1).Invalidate(session.SessionId, "user.logout", Arg.Any<DateTimeOffset>());
        auditTrail.Snapshot().Single().ShouldSatisfyAllConditions(
            evt => evt.EventName.ShouldBe(AuditEventName.UserLogout),
            evt => evt.ActorId.ShouldBe("user-1"),
            evt => evt.CorrelationId.ShouldBe(correlationId));
    }

    private sealed class RecordingLockWindowPresenter(bool unlockResult) : ILockWindowPresenter
    {
        public SessionRecord? PresentedSession { get; private set; }

        public bool ShowLockWindow(LockRuntimeWorkflow workflow, SessionRecord session)
        {
            PresentedSession = session;
            return unlockResult;
        }
    }
}
