using System.Text.Json;

namespace Dobby.Licensing;

public sealed record LicenseArtifact(
    JsonElement Payload,
    LicenseSignatureBlock Signature,
    byte[] CanonicalPayload);
