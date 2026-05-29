using System.Text;
using Dobby.Security.Authentication;
using Shouldly;

namespace Dobby.UnitTests.Recovery;

public sealed class RecoveryCanonicalJsonWriterTests
{
    [Fact]
    public void Write_payload_orders_properties_without_insignificant_whitespace()
    {
        var payload = new RecoveryResponsePayload(
            "dobby.owner-recovery-response.v1",
            "request-1",
            new RecoveryDeviceIdentity("machine", "cpu", "board", "disk", "mac"),
            "2026-05-18T00:00:00Z",
            "2026-05-19T00:00:00Z",
            "nonce-1",
            1);

        var json = Encoding.UTF8.GetString(RecoveryCanonicalJsonWriter.WritePayload(payload));

        json.ShouldBe("{\"deviceIdentity\":{\"cpuId\":\"cpu\",\"diskSerial\":\"disk\",\"macAddress\":\"mac\",\"machineGuid\":\"machine\",\"motherboardSerial\":\"board\"},\"expiresAt\":\"2026-05-19T00:00:00Z\",\"issuedAt\":\"2026-05-18T00:00:00Z\",\"keyVersion\":1,\"nonce\":\"nonce-1\",\"requestId\":\"request-1\",\"type\":\"dobby.owner-recovery-response.v1\"}");
    }

    [Fact]
    public void Write_payload_keeps_key_version_numeric_and_escapes_strings()
    {
        var payload = new RecoveryResponsePayload(
            "type-\"quoted\"",
            "request-1",
            new RecoveryDeviceIdentity("machine", "cpu", "board", "disk", "mac"),
            "2026-05-18T00:00:00Z",
            "2026-05-19T00:00:00Z",
            "line\nnonce",
            1);

        var json = Encoding.UTF8.GetString(RecoveryCanonicalJsonWriter.WritePayload(payload));

        json.ShouldContain("\"keyVersion\":1");
        json.ShouldContain("type-\\\"quoted\\\"");
        json.ShouldContain("line\\nnonce");
        json.ShouldNotContain("\"keyVersion\":\"1\"");
        json.ShouldNotContain(" ");
    }
}
