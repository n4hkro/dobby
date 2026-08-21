namespace Dobby.Licensing;

public sealed record LicenseSignatureBlock(
    string Purpose,
    string Algorithm,
    string Format,
    string KeyId,
    string Value);
