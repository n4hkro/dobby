using Dobby.Audit;
using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Audit;

public sealed class AuditDetailsSanitizerTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("passwordHash")]
    [InlineData("token")]
    [InlineData("privateKey")]
    [InlineData("sessionMaterial")]
    [InlineData("rememberMeSecret")]
    [InlineData("recoveryNonce")]
    [InlineData("recoveryResponse")]
    [InlineData("licensePayload")]
    public void Append_rejects_raw_secret_detail_keys(string forbiddenKey)
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var auditEvent = new AuditEvent(
            AuditEventName.UserLoginFailed,
            "owner",
            "login",
            AuditResult.Failed,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { [forbiddenKey] = "raw-secret" });

        Should.Throw<InvalidOperationException>(() => trail.Append(auditEvent))
            .Message.ShouldContain(forbiddenKey, Case.Insensitive);
    }

    [Fact]
    public void Append_allows_support_safe_detail_keys_and_azerbaijani_text()
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var auditEvent = new AuditEvent(
            AuditEventName.DiagnosticsExportCreated,
            "owner",
            "support-package",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["fileCount"] = "3", ["note"] = "şəxsi məlumat yoxdur" });

        trail.Append(auditEvent);

        trail.Snapshot().Single().Details!["note"].ShouldBe("şəxsi məlumat yoxdur");
    }

    [Theory]
    [MemberData(nameof(ForbiddenAssignmentDetails))]
    public void Append_rejects_assignment_shaped_raw_secret_detail_values(string detailValue)
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var auditEvent = new AuditEvent(
            AuditEventName.SecurityVerificationFailed,
            "system",
            "startup",
            AuditResult.Failed,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["message"] = detailValue });

        Should.Throw<InvalidOperationException>(() => trail.Append(auditEvent))
            .Message.ShouldContain("message", Case.Insensitive);
    }

    public static IEnumerable<object[]> ForbiddenAssignmentDetails()
    {
        yield return ["password" + "=secret"];
        yield return ["passwordHash=hash"];
        yield return ["token=abc"];
        yield return ["privateKey=key"];
        yield return ["signature=sig"];
        yield return ["sessionMaterial=session"];
        yield return ["rememberMeSecret=remember"];
        yield return ["recoveryNonce=nonce"];
        yield return ["recoveryResponse=response"];
        yield return ["licensePayload=payload"];
        yield return ["licenseArtifact=artifact"];
        yield return ["licenseBinding=binding"];
    }

    [Fact]
    public void Append_allows_owner_bootstrap_safe_details()
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var details = new Dictionary<string, string>
        {
            ["bootstrapKind"] = "first-user-owner",
            ["principalRole"] = "Owner",
            ["permissionState"] = "is-owner-short-circuit",
            ["composition"] = "B04-transaction-aware-host-audit"
        };
        var auditEvent = new AuditEvent(
            AuditEventName.UserCreate,
            "system",
            "user-1",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            details);

        trail.Append(auditEvent);

        trail.Snapshot().Single().Details.ShouldBe(details);
    }
}
