using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class SelfServicePasswordChangeCoordinator(
    IHostPersistenceCommandExecutor executor,
    CredentialStore credentialStore,
    RememberMeSlotService rememberMeSlotService,
    IAuditTrail auditTrail)
{
    public Result ChangePassword(string userId, string sessionId, string newPassword, bool rememberMe, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var now = DateTimeOffset.UtcNow;
        var activeSessionExists = executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE SessionId = @SessionId AND UserId = @UserId AND InvalidatedAtUtc IS NULL AND ExpiresAtUtc > @Now;",
            new { SessionId = sessionId, UserId = userId, Now = now.ToString("O") }) == 1;
        if (!activeSessionExists)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var rebind = credentialStore.RebindPassword(userId, newPassword, now);
        if (!rebind.IsSuccess)
        {
            return Result.Failure(rebind.ErrorCode!.Value);
        }

        var affected = executor.Execute(
            "UPDATE Sessions SET SessionVersion = SessionVersion + 1, LastActivityAtUtc = @UpdatedAtUtc WHERE SessionId = @SessionId AND UserId = @UserId AND InvalidatedAtUtc IS NULL;",
            new { SessionId = sessionId, UserId = userId, UpdatedAtUtc = now.ToString("O") });
        if (affected == 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var username = executor.Query<string>("SELECT UsernameNormalized FROM Users WHERE UserId = @UserId;", new { UserId = userId }).Single();
        var remember = rememberMeSlotService.ApplySuccessfulLogin(userId, username, newPassword, rememberMe);
        if (!remember.IsSuccess)
        {
            return Result.Failure(remember.ErrorCode!.Value);
        }

        auditTrail.Append(new AuditEvent(
            AuditEventName.UserPasswordChange,
            userId,
            userId,
            AuditResult.Success,
            correlationId,
            now,
            new Dictionary<string, string> { ["state"] = "active-preserved" }));

        return Result.Success();
    }
}
