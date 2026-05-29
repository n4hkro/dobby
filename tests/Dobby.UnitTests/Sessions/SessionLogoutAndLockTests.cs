using Dobby.Configuration;
using Dobby.Core;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Sessions;

public sealed class SessionLogoutAndLockTests
{
    [Fact]
    public void Unlock_requires_matching_password_before_clearing_lock_state()
    {
        var credentials = Substitute.For<IUserCredentialVerifier>();
        credentials.VerifyPassword("user-1", "wrong password").Returns(Result.Success(false));
        credentials.VerifyPassword("user-1", "correct password").Returns(Result.Success(true));
        var service = CreateService(credentials);
        service.Create("user-1").IsSuccess.ShouldBeTrue();
        service.Lock().IsSuccess.ShouldBeTrue();

        var wrong = service.Unlock("wrong password");

        wrong.IsSuccess.ShouldBeFalse();
        wrong.ErrorCode.ShouldBe(ErrorCode.Unauthorized);
        service.IsLocked.ShouldBeTrue();

        var correct = service.Unlock("correct password");

        correct.IsSuccess.ShouldBeTrue();
        service.IsLocked.ShouldBeFalse();
        credentials.Received(1).VerifyPassword("user-1", "wrong password");
        credentials.Received(1).VerifyPassword("user-1", "correct password");
    }

    [Fact]
    public void Unlock_without_active_session_returns_not_found_without_checking_credentials()
    {
        var credentials = Substitute.For<IUserCredentialVerifier>();
        var service = CreateService(credentials);

        var result = service.Unlock("correct password");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.NotFound);
        credentials.DidNotReceiveWithAnyArgs().VerifyPassword(default!, default!);
    }

    [Fact]
    public void Unlock_preserves_specific_credential_failure_and_remains_locked()
    {
        var credentials = Substitute.For<IUserCredentialVerifier>();
        credentials.VerifyPassword("user-1", "correct password").Returns(Result.Failure<bool>(ErrorCode.DatabaseUnavailable));
        var service = CreateService(credentials);
        service.Create("user-1").IsSuccess.ShouldBeTrue();
        service.Lock().IsSuccess.ShouldBeTrue();

        var result = service.Unlock("correct password");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        service.IsLocked.ShouldBeTrue();
    }

    [Fact]
    public void Auto_lock_checkpoint_only_marks_first_locked_poll_as_transitioned()
    {
        var service = CreateService(Substitute.For<IUserCredentialVerifier>(), autoLock: true);
        service.Create("user-1").IsSuccess.ShouldBeTrue();
        Time.Advance(TimeSpan.FromMinutes(30));

        var first = service.Checkpoint();
        var second = service.Checkpoint();

        first.Kind.ShouldBe(SessionCheckpointKind.Locked);
        first.JustTransitioned.ShouldBeTrue();
        second.Kind.ShouldBe(SessionCheckpointKind.Locked);
        second.JustTransitioned.ShouldBeFalse();
    }

    private FakeTimeProvider Time { get; } = new(DateTimeOffset.Parse("2026-05-18T00:00:00Z"));

    private SessionLifecycleService CreateService(IUserCredentialVerifier credentials, bool autoLock = false)
    {
        var store = Substitute.For<ISessionStore>();
        store.IsUserActive("user-1").Returns(true);

        return new SessionLifecycleService(
            store,
            new ActiveSessionAccessor(),
            credentials,
            Time,
            new SessionOptions { AbsoluteTimeoutMinutes = 480, IdleTimeoutMinutes = 30, AutoLockEnabledByDefault = autoLock });
    }
}
