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
            .Message.ShouldBe("Audit detail field is not approved for this event.");
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
            new Dictionary<string, string> { ["included"] = "3", ["outputName"] = "dəstək.zip" });

        trail.Append(auditEvent);

        trail.Snapshot().Single().Details!["outputName"].ShouldBe("dəstək.zip");
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
            new Dictionary<string, string> { ["reason"] = detailValue });

        Should.Throw<InvalidOperationException>(() => trail.Append(auditEvent))
            .Message.ShouldBe("Audit detail value is malformed or contains unapproved diagnostic data.");
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

        var saved = trail.Snapshot().Single().Details!;
        saved.Count.ShouldBe(details.Count);
        foreach (var detail in details)
        {
            saved[detail.Key].ShouldBe(detail.Value);
        }
    }

    [Theory]
    [InlineData("C:\\private\\artifact.json")]
    [InlineData("\\\\server\\share\\artifact.json")]
    [InlineData("-----BEGIN PRIVATE KEY-----raw-----END PRIVATE KEY-----")]
    [InlineData("SELECT PasswordHash FROM Users")]
    [InlineData("line-one\nline-two")]
    [InlineData("line-one\u2028line-two")]
    public void Append_rejects_paths_private_material_sql_and_multiline_values(string unsafeValue)
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var auditEvent = new AuditEvent(
            AuditEventName.SecurityVerificationFailed,
            "system",
            "startup",
            AuditResult.Failed,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["reason"] = unsafeValue });

        Should.Throw<InvalidOperationException>(() => trail.Append(auditEvent));
        trail.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Append_rejects_unknown_fields_and_oversized_values_at_the_boundary()
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var unknown = new AuditEvent(
            AuditEventName.DiagnosticsExportCreated,
            "owner",
            "diagnostics.export",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow,
            new Dictionary<string, string> { ["note"] = "not-approved" });
        var oversized = unknown with
        {
            Details = new Dictionary<string, string> { ["outputName"] = new string('x', 257) }
        };

        Should.Throw<InvalidOperationException>(() => trail.Append(unknown));
        Should.Throw<InvalidOperationException>(() => trail.Append(oversized));
        trail.Snapshot().ShouldBeEmpty();
    }
}
