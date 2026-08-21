namespace Dobby.Security.Trust;

public sealed record PluginTrustPayload(
    string VendorId,
    string Purpose,
    string Algorithm,
    string Format,
    string KeyId,
    byte[] CanonicalPayload,
    string SignatureValue);

public sealed record PluginTrustPublicKey(
    string ResourceName,
    DobbyPluginTrustProfile Profile,
    string Purpose,
    string KeyId,
    byte[] PublicKeyBytes,
    string Sha256,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset? RetiredAtUtc,
    bool IsTestOnly);

public sealed record PluginTrustVerificationResult(
    bool IsSuccess,
    string Code,
    string Message)
{
    public static PluginTrustVerificationResult Success() => new(true, string.Empty, string.Empty);

    public static PluginTrustVerificationResult Failure(string code, string message) => new(false, code, message);
}

public sealed record PluginTrustPublicKeyResolutionResult(
    bool IsSuccess,
    PluginTrustPublicKey? Value,
    string Code,
    string Message)
{
    public static PluginTrustPublicKeyResolutionResult Success(PluginTrustPublicKey value)
    {
        return new PluginTrustPublicKeyResolutionResult(true, value, string.Empty, string.Empty);
    }

    public static PluginTrustPublicKeyResolutionResult Failure(string code, string message)
    {
        return new PluginTrustPublicKeyResolutionResult(false, null, code, message);
    }
}
