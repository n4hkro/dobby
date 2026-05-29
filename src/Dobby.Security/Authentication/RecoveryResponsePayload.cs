using System.Text.Json.Serialization;

namespace Dobby.Security.Authentication;

public sealed record RecoveryResponsePayload(
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("requestId")]
    string RequestId,
    [property: JsonPropertyName("deviceIdentity")]
    RecoveryDeviceIdentity DeviceIdentity,
    [property: JsonPropertyName("issuedAt")]
    string IssuedAt,
    [property: JsonPropertyName("expiresAt")]
    string ExpiresAt,
    [property: JsonPropertyName("nonce")]
    string Nonce,
    [property: JsonPropertyName("keyVersion")]
    int KeyVersion);

public sealed record RecoveryResponseEnvelope(
    [property: JsonPropertyName("payload")]
    RecoveryResponsePayload Payload,
    [property: JsonPropertyName("signature")]
    string Signature);
