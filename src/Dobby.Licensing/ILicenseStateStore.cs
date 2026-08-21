namespace Dobby.Licensing;

public interface ILicenseStateStore
{
    LicenseStateRecord? Read(RevisionSeriesKey key);

    void Save(LicenseStateRecord state);

    void AppendAttestation(LicenseImportAttestation attestation);

    LicenseImportCommitResult CommitSuccessfulImport(
        LicenseStateRecord state,
        LicenseImportAttestation attestation,
        LicenseImportCommitMetadata metadata)
    {
        var decision = LicenseImportCommitPolicy.Decide(Read(state.RevisionSeriesKey), state);
        if (decision != LicenseImportCommitOutcome.Committed)
        {
            return new LicenseImportCommitResult(decision, SuccessAuditAlreadyPresent: decision == LicenseImportCommitOutcome.AlreadyCurrent);
        }

        Save(state);
        AppendAttestation(attestation);
        return new LicenseImportCommitResult(LicenseImportCommitOutcome.Committed, SuccessAuditAlreadyPresent: false);
    }
}

public sealed record LicenseImportCommitMetadata(
    string ActorId,
    string CorrelationId);

public enum LicenseImportCommitOutcome
{
    Committed,
    AlreadyCurrent,
    OlderOrConflictingRevision
}

public sealed record LicenseImportCommitResult(
    LicenseImportCommitOutcome Outcome,
    bool SuccessAuditAlreadyPresent);

public static class LicenseImportCommitPolicy
{
    public static LicenseImportCommitOutcome Decide(
        LicenseStateRecord? existing,
        LicenseStateRecord incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (existing is null)
        {
            return LicenseImportCommitOutcome.Committed;
        }

        if (incoming.LicenseRevision < existing.LicenseRevision ||
            RevisionSeriesConflicts(existing, incoming))
        {
            return LicenseImportCommitOutcome.OlderOrConflictingRevision;
        }

        if (incoming.LicenseRevision > existing.LicenseRevision)
        {
            return LicenseImportCommitOutcome.Committed;
        }

        return RepresentsSameSignedGrant(existing, incoming)
            ? LicenseImportCommitOutcome.AlreadyCurrent
            : LicenseImportCommitOutcome.OlderOrConflictingRevision;
    }

    private static bool RevisionSeriesConflicts(LicenseStateRecord existing, LicenseStateRecord incoming)
    {
        return !string.IsNullOrWhiteSpace(existing.RevisionSeriesId) &&
               !string.IsNullOrWhiteSpace(incoming.RevisionSeriesId) &&
               !existing.RevisionSeriesId.Equals(incoming.RevisionSeriesId, StringComparison.Ordinal);
    }

    private static bool RepresentsSameSignedGrant(LicenseStateRecord existing, LicenseStateRecord incoming)
    {
        return existing.RevisionSeriesKey == incoming.RevisionSeriesKey &&
               existing.PluginId.Equals(incoming.PluginId, StringComparison.Ordinal) &&
               existing.CustomerId.Equals(incoming.CustomerId, StringComparison.Ordinal) &&
               existing.DeploymentId.Equals(incoming.DeploymentId, StringComparison.Ordinal) &&
               existing.DeviceFingerprint.Equals(incoming.DeviceFingerprint, StringComparison.Ordinal) &&
               existing.PackageFingerprint.Equals(incoming.PackageFingerprint, StringComparison.Ordinal) &&
               existing.LicenseRevision == incoming.LicenseRevision &&
               existing.NotBeforeUtc == incoming.NotBeforeUtc &&
               existing.ExpiresAtUtc == incoming.ExpiresAtUtc &&
               existing.GracePeriodDays == incoming.GracePeriodDays &&
               string.Equals(existing.LicenseId, incoming.LicenseId, StringComparison.Ordinal) &&
               string.Equals(existing.RevisionSeriesId, incoming.RevisionSeriesId, StringComparison.Ordinal) &&
               string.Equals(existing.KeyId, incoming.KeyId, StringComparison.Ordinal) &&
               existing.IssuedAtUtc == incoming.IssuedAtUtc &&
               existing.SourceIntent == incoming.SourceIntent;
    }
}
