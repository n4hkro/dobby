using System.Text.Json.Serialization;
using Dobby.Security.Trust;

namespace Dobby.Security.Authentication;

public sealed record RecoveryResponsePayload(
    [property: JsonPropertyName("schemaVersion")]
    int SchemaVersion,
    [property: JsonPropertyName("purpose")]
    string Purpose,
    [property: JsonPropertyName("keyId")]
    string KeyId,
    [property: JsonPropertyName("responseId")]
    string ResponseId,
    [property: JsonPropertyName("requestId")]
    string RequestId,
    [property: JsonPropertyName("nonce")]
    string Nonce,
    [property: JsonPropertyName("customerId")]
    string CustomerId,
    [property: JsonPropertyName("deploymentId")]
    string DeploymentId,
    [property: JsonPropertyName("deviceId")]
    string DeviceId,
    [property: JsonPropertyName("recoveryPublicKeyHash")]
    string RecoveryPublicKeyHash,
    [property: JsonPropertyName("action")]
    string Action,
    [property: JsonPropertyName("issuedAt")]
    string IssuedAt,
    [property: JsonPropertyName("expiresAt")]
    string ExpiresAt);

public sealed record RecoveryResponseEnvelope(
    [property: JsonPropertyName("payload")]
    RecoveryResponsePayload Payload,
    [property: JsonPropertyName("signature")]
    ArtifactSignatureEnvelope Signature);
