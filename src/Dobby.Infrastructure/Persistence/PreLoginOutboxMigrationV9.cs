namespace Dobby.Infrastructure.Persistence;

internal static class PreLoginOutboxMigrationV9
{
    public const string MigrationId = "0009_prelogin_host_outbox";

    public const string Sql = """
        CREATE TABLE HostOutbox (
            OutboxId TEXT PRIMARY KEY,
            IdempotencyKey TEXT NOT NULL UNIQUE CHECK (length(IdempotencyKey) BETWEEN 1 AND 160),
            Purpose TEXT NOT NULL CHECK (length(Purpose) BETWEEN 1 AND 80),
            MessageType TEXT NOT NULL CHECK (length(MessageType) BETWEEN 1 AND 120),
            PayloadJson TEXT NOT NULL CHECK (length(PayloadJson) BETWEEN 2 AND 16384),
            CreatedAtUtc TEXT NOT NULL,
            NextAttemptAtUtc TEXT NOT NULL,
            AttemptCount INTEGER NOT NULL DEFAULT 0 CHECK (AttemptCount BETWEEN 0 AND 100000),
            CompletedAtUtc TEXT,
            LastErrorCode TEXT CHECK (LastErrorCode IS NULL OR length(LastErrorCode) BETWEEN 1 AND 64),
            LeaseId TEXT,
            LeaseExpiresAtUtc TEXT,
            CHECK ((LeaseId IS NULL AND LeaseExpiresAtUtc IS NULL) OR
                   (LeaseId IS NOT NULL AND LeaseExpiresAtUtc IS NOT NULL))
        );
        CREATE INDEX IX_HostOutbox_Dispatch
            ON HostOutbox(CompletedAtUtc, NextAttemptAtUtc, LeaseExpiresAtUtc, CreatedAtUtc);
        """;
}
