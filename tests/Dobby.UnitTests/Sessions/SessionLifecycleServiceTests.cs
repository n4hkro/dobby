using Dobby.Configuration;
using Dobby.Core;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Sessions;

public sealed class SessionLifecycleServiceTests
{
    [Fact]
    public void Create_persists_session_before_publishing_active_memory_state()
    {
        var store = Substitute.For<ISessionStore>();
        var accessor = new ActiveSessionAccessor();
        store.When(candidate => candidate.Create(Arg.Any<SessionRecord>()))
            .Do(_ => accessor.Current.ShouldBeNull());
        var service = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            TimeProvider.System,
            new SessionOptions());

        var result = service.Create("user-1");

        result.IsSuccess.ShouldBeTrue();
        accessor.Current.ShouldBe(result.Value);
    }

    [Fact]
    public void Create_store_failure_does_not_publish_active_memory_state()
    {
        var store = Substitute.For<ISessionStore>();
        var accessor = new ActiveSessionAccessor();
        store.When(candidate => candidate.Create(Arg.Any<SessionRecord>()))
            .Do(_ => throw new InvalidOperationException("Synthetic durable session failure."));
        var service = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            TimeProvider.System,
            new SessionOptions());

        Should.Throw<InvalidOperationException>(() => service.Create("user-1"));

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void Create_lock_unlock_and_logout_manage_active_session_state()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var credentials = Substitute.For<IUserCredentialVerifier>();
        credentials.VerifyPassword("user-1", "password").Returns(Result.Success(true));
        var service = new SessionLifecycleService(store, accessor, credentials, time, new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });

        var created = service.Create("user-1").Value;
        service.Lock().IsSuccess.ShouldBeTrue();
        service.IsLocked.ShouldBeTrue();
        service.Unlock("password").IsSuccess.ShouldBeTrue();
        service.IsLocked.ShouldBeFalse();
        service.Logout("user.logout").IsSuccess.ShouldBeTrue();

        created.UserId.ShouldBe("user-1");
        accessor.Current.ShouldBeNull();
        store.Received(1).Create(created);
        store.Received(1).Invalidate(created.SessionId, "user.logout", Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void Activate_existing_session_sets_active_context_without_creating_duplicate_store_record()
    {
        var store = Substitute.For<ISessionStore>();
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var service = new SessionLifecycleService(
            store,
            accessor,
            Substitute.For<IUserCredentialVerifier>(),
            time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var session = new SessionRecord(
            "session-1",
            "user-1",
            time.GetUtcNow(),
            time.GetUtcNow(),
            time.GetUtcNow().AddHours(8),
            null,
            1);

        service.ActivateExisting(session).IsSuccess.ShouldBeTrue();
        service.ActivateExisting(session).IsSuccess.ShouldBeTrue();

        accessor.Current.ShouldBe(session);
        store.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public void Idle_elapsed_auto_locks_when_enabled_without_invalidating_session()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var service = new SessionLifecycleService(store, accessor, Substitute.For<IUserCredentialVerifier>(), time, new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = true });
        service.Create("user-1");

        time.Advance(TimeSpan.FromMinutes(30));
        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Locked);
        checkpoint.JustTransitioned.ShouldBeTrue();
        service.IsLocked.ShouldBeTrue();
        store.DidNotReceiveWithAnyArgs().Invalidate(default!, default!, default);
    }

    [Fact]
    public void Idle_elapsed_without_auto_lock_invalidates_session()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var service = new SessionLifecycleService(store, accessor, Substitute.For<IUserCredentialVerifier>(), time, new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var created = service.Create("user-1").Value;

        time.Advance(TimeSpan.FromMinutes(30));
        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Expired);
        checkpoint.Reason.ShouldBe("idle");
        accessor.Current.ShouldBeNull();
        store.Received(1).Invalidate(created.SessionId, "idle", Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void Recorded_activity_resets_idle_timeout_window()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var service = new SessionLifecycleService(store, accessor, Substitute.For<IUserCredentialVerifier>(), time, new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30 });
        var created = service.Create("user-1").Value;

        time.Advance(TimeSpan.FromMinutes(20));
        service.RecordActivity().IsSuccess.ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(20));
        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Active);
        accessor.Current.ShouldNotBeNull();
        store.Received(1).UpdateLastActivity(created.SessionId, time.GetUtcNow().AddMinutes(-20));
        store.DidNotReceive().Invalidate(created.SessionId, "idle", Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public void Unlock_resets_idle_timeout_window_after_auto_lock()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var credentials = Substitute.For<IUserCredentialVerifier>();
        credentials.VerifyPassword("user-1", "password").Returns(Result.Success(true));
        var service = new SessionLifecycleService(store, accessor, credentials, time, new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = true });
        var created = service.Create("user-1").Value;
        time.Advance(TimeSpan.FromMinutes(30));
        service.Checkpoint().Kind.ShouldBe(SessionCheckpointKind.Locked);

        service.Unlock("password").IsSuccess.ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(1));
        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Active);
        store.Received(1).UpdateLastActivity(created.SessionId, time.GetUtcNow().AddMinutes(-1));
    }

    [Fact]
    public void Absolute_elapsed_invalidates_session_even_when_auto_lock_enabled()
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);
        var accessor = new ActiveSessionAccessor();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));
        var service = new SessionLifecycleService(store, accessor, Substitute.For<IUserCredentialVerifier>(), time, new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = true });
        var created = service.Create("user-1").Value;

        time.Advance(TimeSpan.FromMinutes(480));
        var checkpoint = service.Checkpoint();

        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Expired);
        checkpoint.Reason.ShouldBe("absolute");
        store.Received(1).Invalidate(created.SessionId, "absolute", Arg.Any<DateTimeOffset>());
    }
}
