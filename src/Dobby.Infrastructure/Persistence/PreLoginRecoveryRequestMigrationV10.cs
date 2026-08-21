namespace Dobby.Infrastructure.Persistence;

internal static class PreLoginRecoveryRequestMigrationV10
{
    public const string MigrationId = "0010_prelogin_recovery_requests";

    public const string Sql = """
        CREATE TABLE RecoveryRequests (
            RequestId TEXT PRIMARY KEY CHECK (length(RequestId) = 36),
            NonceHash TEXT NOT NULL UNIQUE CHECK (length(NonceHash) = 64),
            CustomerId TEXT NOT NULL CHECK (length(CustomerId) BETWEEN 1 AND 128),
            DeploymentId TEXT NOT NULL CHECK (length(DeploymentId) BETWEEN 1 AND 128),
            DeviceId TEXT NOT NULL CHECK (length(DeviceId) BETWEEN 1 AND 128),
            RecoveryPublicKeyHash TEXT NOT NULL CHECK (length(RecoveryPublicKeyHash) = 64),
            VendorKeyId TEXT NOT NULL CHECK (length(VendorKeyId) BETWEEN 3 AND 64),
            PayloadHash TEXT NOT NULL CHECK (length(PayloadHash) = 64),
            IssuedAtUtc TEXT NOT NULL,
            ExpiresAtUtc TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            ConsumedAtUtc TEXT,
            ResponseId TEXT UNIQUE CHECK (ResponseId IS NULL OR length(ResponseId) = 36),
            CHECK ((ConsumedAtUtc IS NULL AND ResponseId IS NULL) OR
                   (ConsumedAtUtc IS NOT NULL AND ResponseId IS NOT NULL))
        );
        CREATE INDEX IX_RecoveryRequests_Expiry
            ON RecoveryRequests(ConsumedAtUtc, ExpiresAtUtc, CreatedAtUtc);
        """;
}
