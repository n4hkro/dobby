namespace Dobby.Licensing;

public sealed record LicensePayloadValidationResult(
    bool IsSuccess,
    LicensePayload? Payload,
    string ReasonCode)
{
    public static LicensePayloadValidationResult Success(LicensePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new LicensePayloadValidationResult(true, payload, string.Empty);
    }

    public static LicensePayloadValidationResult Failure(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new LicensePayloadValidationResult(false, null, reasonCode);
    }
}
