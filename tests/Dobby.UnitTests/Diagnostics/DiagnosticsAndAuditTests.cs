using Dobby.Audit;
using Dobby.Core;
using Dobby.Diagnostics;
using Shouldly;

namespace Dobby.UnitTests.Diagnostics;

public sealed class DiagnosticsAndAuditTests
{
    [Fact]
    public void Diagnostics_redactor_removes_sensitive_values()
    {
        var redacted = DiagnosticRedactor.Redact("password=secret; deviceFingerprint=abc; ok=value");

        redacted.ShouldContain("password=<redacted>");
        redacted.ShouldContain("deviceFingerprint=<redacted>");
        redacted.ShouldContain("ok=value");
    }

    [Fact]
    public void Audit_trail_is_append_only_from_public_boundary()
    {
        IAuditTrail trail = new InMemoryAuditTrail();
        var evt = new AuditEvent(
            AuditEventName.UserLogin,
            "owner",
            "host",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.UtcNow);

        trail.Append(evt);

        trail.Snapshot().ShouldBe([evt]);
    }
}
