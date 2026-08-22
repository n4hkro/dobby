namespace Dobby.Infrastructure.Persistence;

internal static class PreLoginOutboxEvidenceMigrationV11
{
    public const string MigrationId = "0011_prelogin_outbox_evidence";

    public const string Sql = """
        ALTER TABLE HostOutbox
            ADD COLUMN EvidenceCorrelationId TEXT
            CHECK (EvidenceCorrelationId IS NULL OR length(EvidenceCorrelationId) BETWEEN 1 AND 64);
        CREATE INDEX IF NOT EXISTS IX_HostOutbox_PendingEvidence
            ON HostOutbox(CompletedAtUtc, EvidenceCorrelationId);
        """;
}
