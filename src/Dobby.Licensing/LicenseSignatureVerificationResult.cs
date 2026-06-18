namespace Dobby.Licensing;

public sealed record LicenseSignatureVerificationResult(
    bool IsSuccess,
    string ReasonCode,
    string SupportCode)
{
    public static LicenseSignatureVerificationResult Success()
    {
        return new LicenseSignatureVerificationResult(true, string.Empty, string.Empty);
    }

    public static LicenseSignatureVerificationResult Failure(string reasonCode, string supportCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(supportCode);
        return new LicenseSignatureVerificationResult(false, reasonCode, supportCode);
    }
}
