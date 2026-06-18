namespace Dobby.Licensing;

public sealed record LicenseSignatureBlock(
    string Algorithm,
    int KeyVersion,
    string Value);
