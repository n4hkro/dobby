using System.Text;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class RecoveryCanonicalJsonWriterTests
{
    [Fact]
    public void Write_payload_orders_v2_properties_without_insignificant_whitespace()
    {
        var payload = Payload();

        var json = Encoding.UTF8.GetString(RecoveryCanonicalJsonWriter.WritePayload(payload));

        json.ShouldBe("{\"action\":\"reset-owner-password\",\"customerId\":\"customer-1\",\"deploymentId\":\"deployment-1\",\"deviceId\":\"device:abc\",\"expiresAt\":\"2026-08-22T09:00:00.0000000Z\",\"issuedAt\":\"2026-08-21T09:00:00.0000000Z\",\"keyId\":\"recovery-test-2026-01\",\"nonce\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\",\"purpose\":\"dobby.owner-recovery-response\",\"recoveryPublicKeyHash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"requestId\":\"018e51be-7f65-7bcd-8000-000000000002\",\"responseId\":\"018e51be-7f65-7bcd-8000-000000000001\",\"schemaVersion\":2}");
    }

    [Fact]
    public void Canonical_writer_preserves_integral_version_and_escapes_strings()
    {
        var json = Encoding.UTF8.GetString(RecoveryCanonicalJsonWriter.WritePayload(
            Payload() with { CustomerId = "customer-\"quoted\"" }));

        json.ShouldContain("\"schemaVersion\":2");
        json.ShouldContain("customer-\\u0022quoted\\u0022");
        json.ShouldNotContain("\"schemaVersion\":\"2\"");
        json.ShouldNotContain(" ");
    }

    private static RecoveryResponsePayload Payload()
    {
        return new RecoveryResponsePayload(
            2,
            ArtifactSignaturePurposes.OwnerRecoveryResponse,
            "recovery-test-2026-01",
            "018e51be-7f65-7bcd-8000-000000000001",
            "018e51be-7f65-7bcd-8000-000000000002",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "customer-1",
            "deployment-1",
            "device:abc",
            new string('a', 64),
            "reset-owner-password",
            "2026-08-21T09:00:00.0000000Z",
            "2026-08-22T09:00:00.0000000Z");
    }
}
