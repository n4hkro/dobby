using Dobby.Audit;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Infrastructure.Persistence;
using Dobby.Licensing;

namespace Dobby.Host;

public sealed class HostLicenseStateStore(
    IHostPersistenceCommandExecutor executor,
    IHostPersistenceTransactionScope transactionScope,
    IHostMutationUnitOfWork? mutationUnitOfWork = null) : ILicenseStateStore
{
    public LicenseStateRecord? Read(RevisionSeriesKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return ReadInScope(executor, key);
    }

    private static LicenseStateRecord? ReadInScope(
        IHostPersistenceCommandExecutor targetExecutor,
        RevisionSeriesKey key)
    {
        var row = targetExecutor.Query<LicenseStateRow>(
            """
            SELECT
                PluginId, CustomerId, DeploymentId, DeviceFingerprint, PackageFingerprint,
                LicenseRevision, LifecycleState, ImportedAtUtc,
                NotBeforeUtc, ExpiresAtUtc, GracePeriodDays, SourceStatus,
                LicenseId, RevisionSeriesId, KeyId, IssuedAtUtc, SourceIntent, TrustedTimeHealth
            FROM PluginLicenseState
            WHERE PluginId = @PluginId
              AND CustomerId = @CustomerId
              AND DeploymentId = @DeploymentId;
            """,
            key)
            .SingleOrDefault();

        return row?.TryToState();
    }

    public void Save(LicenseStateRecord state)
    {
        ArgumentNullException.ThrowIfNull(state);

        transactionScope.ExecuteInTransaction(tx =>
        {
            SaveInScope(tx, state);
        });
    }

    private static void SaveInScope(
        IHostPersistenceCommandExecutor tx,
        LicenseStateRecord state)
    {
        tx.Execute(
                """
                INSERT INTO PluginLicenseState (
                    PluginId, CustomerId, DeploymentId, DeviceFingerprint, PackageFingerprint,
                    LicenseRevision, LifecycleState, ImportedAtUtc,
                    NotBeforeUtc, ExpiresAtUtc, GracePeriodDays, SourceStatus,
                    LicenseId, RevisionSeriesId, KeyId, IssuedAtUtc, SourceIntent, TrustedTimeHealth
                ) VALUES (
                    @PluginId, @CustomerId, @DeploymentId, @DeviceFingerprint, @PackageFingerprint,
                    @LicenseRevision, @LifecycleState, @ImportedAtUtc,
                    @NotBeforeUtc, @ExpiresAtUtc, @GracePeriodDays, @SourceStatus,
                    @LicenseId, @RevisionSeriesId, @KeyId, @IssuedAtUtc, @SourceIntent, @TrustedTimeHealth
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
                    SourceStatus = excluded.SourceStatus,
                    LicenseId = excluded.LicenseId,
                    RevisionSeriesId = excluded.RevisionSeriesId,
                    KeyId = excluded.KeyId,
                    IssuedAtUtc = excluded.IssuedAtUtc,
                    SourceIntent = excluded.SourceIntent,
                    TrustedTimeHealth = excluded.TrustedTimeHealth;
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
                    SourceStatus = state.SourceStatus?.ToString(),
                    state.LicenseId,
                    state.RevisionSeriesId,
                    state.KeyId,
                    IssuedAtUtc = state.IssuedAtUtc?.ToString("O"),
                    SourceIntent = state.SourceIntent?.ToString(),
                    TrustedTimeHealth = state.TrustedTimeHealth.ToString()
                });
    }

    public void AppendAttestation(LicenseImportAttestation attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);

        AppendAttestationInScope(executor, attestation);
    }

    public LicenseImportCommitResult CommitSuccessfulImport(
        LicenseStateRecord state,
        LicenseImportAttestation attestation,
        LicenseImportCommitMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(attestation);
        ArgumentNullException.ThrowIfNull(metadata);

        if (mutationUnitOfWork is null)
        {
            LicenseImportCommitResult? result = null;
            transactionScope.ExecuteInTransaction(tx =>
            {
                var decision = LicenseImportCommitPolicy.Decide(ReadInScope(tx, state.RevisionSeriesKey), state);
                if (decision != LicenseImportCommitOutcome.Committed)
                {
                    result = new LicenseImportCommitResult(
                        decision,
                        SuccessAuditAlreadyPresent: decision == LicenseImportCommitOutcome.AlreadyCurrent);
                    return;
                }

                SaveInScope(tx, state);
                AppendAttestationInScope(tx, attestation);
                result = new LicenseImportCommitResult(
                    LicenseImportCommitOutcome.Committed,
                    SuccessAuditAlreadyPresent: false);
            });
            return result ?? throw new InvalidOperationException("License import transaction did not complete.");
        }

        if (!Guid.TryParse(metadata.CorrelationId, out var correlationGuid) ||
            string.IsNullOrWhiteSpace(metadata.ActorId))
        {
            throw new InvalidOperationException("License import commit metadata is invalid.");
        }

        return mutationUnitOfWork.Execute(context =>
        {
            var decision = LicenseImportCommitPolicy.Decide(
                ReadInScope(context.Commands, state.RevisionSeriesKey),
                state);
            if (decision != LicenseImportCommitOutcome.Committed)
            {
                return new LicenseImportCommitResult(
                    decision,
                    SuccessAuditAlreadyPresent: decision == LicenseImportCommitOutcome.AlreadyCurrent);
            }

            SaveInScope(context.Commands, state);
            AppendAttestationInScope(context.Commands, attestation);
            context.AppendAudit(new AuditEvent(
                AuditEventName.LicenseImportSucceeded,
                metadata.ActorId,
                $"license:{state.PluginId}",
                AuditResult.Success,
                new CorrelationId(correlationGuid),
                state.ImportedAtUtc,
                DiagnosticRedactor.RedactFields(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reasonCode"] = "succeeded",
                    ["lifecycleState"] = state.LifecycleState.ToString()
                })));
            context.EnqueueOutbox(new HostOutboxMessage(
                $"license-import:{state.LicenseId}:{state.LicenseRevision}",
                "license",
                "activation-refresh-requested",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["pluginId"] = state.PluginId,
                    ["revisionSeriesId"] = state.RevisionSeriesId ?? string.Empty,
                    ["licenseRevision"] = state.LicenseRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
            return new LicenseImportCommitResult(
                LicenseImportCommitOutcome.Committed,
                SuccessAuditAlreadyPresent: true);
        });
    }

    private static void AppendAttestationInScope(
        IHostPersistenceCommandExecutor targetExecutor,
        LicenseImportAttestation attestation)
    {
        targetExecutor.Execute(
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
        public string? LicenseId { get; set; }
        public string? RevisionSeriesId { get; set; }
        public string? KeyId { get; set; }
        public string? IssuedAtUtc { get; set; }
        public string? SourceIntent { get; set; }
        public string TrustedTimeHealth { get; set; } = string.Empty;

        public LicenseStateRecord? TryToState()
        {
            if (!Enum.TryParse<LicenseLifecycleState>(LifecycleState, ignoreCase: false, out var lifecycle) ||
                !Enum.IsDefined(lifecycle) ||
                !TryParseDateTimeOffset(ImportedAtUtc, out var importedAt) ||
                !TryParseNullableDateTimeOffset(NotBeforeUtc, out var notBefore) ||
                !TryParseNullableDateTimeOffset(ExpiresAtUtc, out var expiresAt) ||
                !TryParseNullableDateTimeOffset(IssuedAtUtc, out var issuedAt) ||
                !Enum.TryParse<TrustedTimeHealth>(TrustedTimeHealth, ignoreCase: false, out var timeHealth) ||
                !Enum.IsDefined(timeHealth) ||
                !TryParseOptionalEnum(SourceStatus, out LicenseArtifactStatus? sourceStatus) ||
                !TryParseOptionalEnum(SourceIntent, out LicenseGrantIntent? sourceIntent))
            {
                return null;
            }

            return new LicenseStateRecord(
                new RevisionSeriesKey(PluginId, CustomerId, DeploymentId),
                PluginId,
                CustomerId,
                DeploymentId,
                DeviceFingerprint,
                PackageFingerprint,
                LicenseRevision,
                lifecycle,
                importedAt,
                notBefore,
                expiresAt,
                GracePeriodDays,
                sourceStatus,
                LicenseId,
                RevisionSeriesId,
                KeyId,
                issuedAt,
                sourceIntent,
                timeHealth);
        }

        private static bool TryParseDateTimeOffset(string? value, out DateTimeOffset timestamp)
        {
            return DateTimeOffset.TryParseExact(
                value,
                "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out timestamp);
        }

        private static bool TryParseNullableDateTimeOffset(string? value, out DateTimeOffset? timestamp)
        {
            timestamp = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!TryParseDateTimeOffset(value, out var parsed))
            {
                return false;
            }

            timestamp = parsed;
            return true;
        }

        private static bool TryParseOptionalEnum<T>(string? value, out T? parsed)
            where T : struct, Enum
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!Enum.TryParse<T>(value, ignoreCase: false, out var candidate) || !Enum.IsDefined(candidate))
            {
                return false;
            }

            parsed = candidate;
            return true;
        }
    }
}
