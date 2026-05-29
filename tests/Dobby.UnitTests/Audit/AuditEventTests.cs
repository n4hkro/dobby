using Dobby.Audit;
using Dobby.Core;
using Shouldly;
using System.Text.Json;

namespace Dobby.UnitTests.Audit;

public sealed class AuditEventTests
{
    [Fact]
    public void Audit_event_maps_to_canonical_payload_fields_and_result_values()
    {
        var occurredAt = DateTimeOffset.Parse("2026-05-13T07:00:00Z");
        var correlationId = CorrelationId.New();
        var auditEvent = new AuditEvent(
            AuditEventName.UserLogin,
            "owner",
            "host",
            AuditResult.Success,
            correlationId,
            occurredAt,
            new Dictionary<string, string> { ["culture"] = "az-AZ" });

        var payload = auditEvent.ToCanonicalPayload();

        payload["timestamp"].ShouldBe("2026-05-13T07:00:00.0000000+00:00");
        payload["actor"].ShouldBe("owner");
        payload["action"].ShouldBe("user.login");
        payload["target"].ShouldBe("host");
        payload["result"].ShouldBe("success");
        payload["correlationId"].ShouldBe(correlationId.ToString());
        var details = JsonSerializer.Deserialize<Dictionary<string, string>>(payload["details"]);
        details.ShouldNotBeNull();
        details["culture"].ShouldBe("az-AZ");
        payload.ShouldNotContainKey("details.culture");
    }

    [Theory]
    [InlineData(AuditResult.Success, "success")]
    [InlineData(AuditResult.Failed, "failed")]
    [InlineData(AuditResult.Denied, "denied")]
    public void Audit_result_uses_lowercase_payload_values(AuditResult result, string expected)
    {
        result.ToPayloadValue().ShouldBe(expected);
    }
}
