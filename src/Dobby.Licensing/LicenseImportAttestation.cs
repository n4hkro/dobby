namespace Dobby.Licensing;

public sealed record LicenseImportAttestation(
    string SourceArtifactHash,
    int? LicenseRevision,
    DateTimeOffset ImportedAtUtc,
    string Result,
    string CorrelationId);
