using Dobby.Audit;
using Dobby.Core;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class LoginAuditCoordinator(IAuditTrail auditTrail, TimeProvider timeProvider)
{
    public void Append(LoginAttemptResult result, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Kind == LoginAttemptKind.Success)
        {
            auditTrail.Append(new AuditEvent(
                AuditEventName.UserLogin,
                result.UserId ?? "system",
                "host-login",
                AuditResult.Success,
                correlationId,
                timeProvider.GetUtcNow()));
            return;
        }

        auditTrail.Append(new AuditEvent(
            AuditEventName.UserLoginFailed,
            "system",
            "host-login",
            AuditResult.Failed,
            correlationId,
            timeProvider.GetUtcNow(),
            new Dictionary<string, string>
            {
                ["reason"] = ToReason(result.Kind)
            }));
    }

    private static string ToReason(LoginAttemptKind kind)
    {
        return kind switch
        {
            LoginAttemptKind.InvalidCredentials => "invalid-credentials",
            LoginAttemptKind.ValidationFailed => "validation-failed",
            LoginAttemptKind.KdfUnsupported => "kdf-unsupported",
            LoginAttemptKind.Deactivated => "deactivated",
            LoginAttemptKind.Throttled => "throttled",
            LoginAttemptKind.Busy => "busy",
            _ => "validation-failed"
        };
    }
}
