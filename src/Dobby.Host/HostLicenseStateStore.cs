using Dobby.Infrastructure.Persistence;
using Dobby.Licensing;

namespace Dobby.Host;

public sealed class HostLicenseStateStore(
    IHostPersistenceCommandExecutor executor,
    IHostPersistenceTransactionScope transactionScope) : ILicenseStateStore
{
    public LicenseStateRecord? Read(RevisionSeriesKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var row = executor.Query<LicenseStateRow>(
            """
            SELECT
                PluginId, CustomerId, DeploymentId, DeviceFingerprint, PackageFingerprint,
                LicenseRevision, LifecycleState, ImportedAtUtc,
                NotBeforeUtc, ExpiresAtUtc, GracePeriodDays, SourceStatus
            FROM PluginLicenseState
            WHERE PluginId = @PluginId
              AND CustomerId = @CustomerId
              AND DeploymentId = @DeploymentId;
            """,
            key)
            .SingleOrDefault();

        return row?.ToState();
    }

    public void Save(LicenseStateRecord state)
    {
        ArgumentNullException.ThrowIfNull(state);

        transactionScope.ExecuteInTransaction(tx =>
        {
            tx.Execute(
                """
                INSERT INTO PluginLicenseState (
                    PluginId, CustomerId, DeploymentId, DeviceFingerprint, PackageFingerprint,
                    LicenseRevision, LifecycleState, ImportedAtUtc,
                    NotBeforeUtc, ExpiresAtUtc, GracePeriodDays, SourceStatus
                ) VALUES (
                    @PluginId, @CustomerId, @DeploymentId, @DeviceFingerprint, @PackageFingerprint,
                    @LicenseRevision, @LifecycleState, @ImportedAtUtc,
                    @NotBeforeUtc, @ExpiresAtUtc, @GracePeriodDays, @SourceStatus
                )
                ON CONFLICT(PluginId, CustomerId, DeploymentId) DO UPDATE SET
                    DeviceFingerprint = excluded.DeviceFingerprint,
                    PackageFingerprint = excluded.PackageFingerprint,
                    LicenseRevision = excluded.LicenseRevision,
                    LifecycleState = excluded.LifecycleState,
                    ImportedAtUtc = excluded.ImportedAtUtc,
                    NotBeforeUtc = excluded.NotBeforeUtc,
                    ExpiresAtUtc = excluded.ExpiresAtUtc,
                    GracePeriodDays = excluded.GracePeriodDays,
                    SourceStatus = excluded.SourceStatus;
                """,
                new
                {
                    state.PluginId,
                    state.CustomerId,
                    state.DeploymentId,
                    state.DeviceFingerprint,
                    state.PackageFingerprint,
                    state.LicenseRevision,
                    LifecycleState = state.LifecycleState.ToString(),
                    ImportedAtUtc = state.ImportedAtUtc.ToString("O"),
                    NotBeforeUtc = state.NotBeforeUtc?.ToString("O"),
                    ExpiresAtUtc = state.ExpiresAtUtc?.ToString("O"),
                    state.GracePeriodDays,
                    SourceStatus = state.SourceStatus?.ToString()
                });
        });
    }

    public void AppendAttestation(LicenseImportAttestation attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        executor.Execute(
            """
            INSERT INTO PluginLicenseImportAttestation (
                AttestationId, SourceArtifactHash, LicenseRevision, ImportedAtUtc, Result, CorrelationId
            ) VALUES (
                @AttestationId, @SourceArtifactHash, @LicenseRevision, @ImportedAtUtc, @Result, @CorrelationId
            );
            """,
            new
            {
                AttestationId = Guid.NewGuid().ToString("N"),
                attestation.SourceArtifactHash,
                attestation.LicenseRevision,
                ImportedAtUtc = attestation.ImportedAtUtc.ToString("O"),
                attestation.Result,
                attestation.CorrelationId
            });
    }

    private sealed class LicenseStateRow
    {
        public string PluginId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string DeploymentId { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        public string PackageFingerprint { get; set; } = string.Empty;
        public int LicenseRevision { get; set; }
        public string LifecycleState { get; set; } = string.Empty;
        public string ImportedAtUtc { get; set; } = string.Empty;
        public string? NotBeforeUtc { get; set; }
        public string? ExpiresAtUtc { get; set; }
        public int? GracePeriodDays { get; set; }
        public string? SourceStatus { get; set; }

        public LicenseStateRecord ToState()
        {
            return new LicenseStateRecord(
                new RevisionSeriesKey(PluginId, CustomerId, DeploymentId),
                PluginId,
                CustomerId,
                DeploymentId,
                DeviceFingerprint,
                PackageFingerprint,
                LicenseRevision,
                Enum.Parse<LicenseLifecycleState>(LifecycleState),
                ParseDateTimeOffset(ImportedAtUtc),
                ParseNullableDateTimeOffset(NotBeforeUtc),
                ParseNullableDateTimeOffset(ExpiresAtUtc),
                GracePeriodDays,
                string.IsNullOrWhiteSpace(SourceStatus)
                    ? null
                    : Enum.Parse<LicenseArtifactStatus>(SourceStatus));
        }

        private static DateTimeOffset ParseDateTimeOffset(string value)
        {
            return DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : ParseDateTimeOffset(value);
        }
    }
}
