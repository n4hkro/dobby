using Dobby.Audit;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class LoginAuditCoordinatorTests
{
    [Theory]
    [InlineData(LoginAttemptKind.InvalidCredentials, "invalid-credentials")]
    [InlineData(LoginAttemptKind.ValidationFailed, "validation-failed")]
    [InlineData(LoginAttemptKind.Deactivated, "deactivated")]
    [InlineData(LoginAttemptKind.Throttled, "throttled")]
    public void Failed_login_attempts_append_user_login_failed_with_safe_reason(LoginAttemptKind kind, string reason)
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new LoginAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));
        var correlationId = CorrelationId.New();

        coordinator.Append(new LoginAttemptResult(kind, RetryAfterSeconds: 10), correlationId);

        var audit = auditTrail.Snapshot().Single();
        audit.EventName.ShouldBe(AuditEventName.UserLoginFailed);
        audit.Result.ShouldBe(AuditResult.Failed);
        audit.ActorId.ShouldBe("system");
        audit.Target.ShouldBe("host-login");
        audit.CorrelationId.ShouldBe(correlationId);
        audit.Details.ShouldNotBeNull();
        audit.Details["reason"].ShouldBe(reason);
    }

    [Fact]
    public void Successful_login_attempt_appends_user_login_success_for_authenticated_user()
    {
        var auditTrail = new InMemoryAuditTrail();
        var coordinator = new LoginAuditCoordinator(auditTrail, new FakeTimeProvider(DateTimeOffset.Parse("2026-05-18T00:00:00Z")));
        var correlationId = CorrelationId.New();

        coordinator.Append(LoginAttemptResult.Success("user-1"), correlationId);

        var audit = auditTrail.Snapshot().Single();
        audit.EventName.ShouldBe(AuditEventName.UserLogin);
        audit.Result.ShouldBe(AuditResult.Success);
        audit.ActorId.ShouldBe("user-1");
        audit.Target.ShouldBe("host-login");
        audit.CorrelationId.ShouldBe(correlationId);
        audit.Details.ShouldBeNull();
    }
}
