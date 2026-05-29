namespace Dobby.Infrastructure.Persistence;

internal static class PreLoginMigrationV1
{
    public const string MigrationId = "0001_prelogin_bootstrap";

    public const string Sql = """
        CREATE TABLE IF NOT EXISTS Users (
            UserId TEXT PRIMARY KEY,
            UsernameNormalized TEXT NOT NULL,
            DisplayName TEXT,
            IsOwner INTEGER NOT NULL CHECK (IsOwner IN (0,1)),
            IsActive INTEGER NOT NULL CHECK (IsActive IN (0,1)),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_UsernameNormalized ON Users(UsernameNormalized);
        CREATE UNIQUE INDEX IF NOT EXISTS UX_Users_SingleOwner ON Users(IsOwner) WHERE IsOwner = 1;

        CREATE TABLE IF NOT EXISTS Credentials (
            CredentialId TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            KdfMode TEXT NOT NULL,
            KdfVersion TEXT,
            PasswordHash BLOB NOT NULL,
            Salt BLOB NOT NULL,
            SaltBitLength INTEGER NOT NULL CHECK (SaltBitLength >= 128),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            PasswordChangedAtUtc TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(UserId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS UX_Credentials_UserId ON Credentials(UserId);

        CREATE TABLE IF NOT EXISTS RememberMeSlot (
            SlotId TEXT PRIMARY KEY,
            UserId TEXT,
            MaskedUsername TEXT,
            ProtectedRememberMeBlob BLOB,
            ProtectionVersion TEXT,
            CreatedAtUtc TEXT,
            UpdatedAtUtc TEXT,
            ExpiresAtUtc TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(UserId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS UX_RememberMeSlot_SlotId ON RememberMeSlot(SlotId);
        CREATE INDEX IF NOT EXISTS IX_RememberMeSlot_UserId ON RememberMeSlot(UserId);

        CREATE TABLE IF NOT EXISTS Sessions (
            SessionId TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            LastActivityAtUtc TEXT NOT NULL,
            ExpiresAtUtc TEXT NOT NULL,
            InvalidatedAtUtc TEXT,
            InvalidationReason TEXT,
            SessionVersion INTEGER NOT NULL CHECK (SessionVersion >= 1),
            FOREIGN KEY (UserId) REFERENCES Users(UserId)
        );
        CREATE INDEX IF NOT EXISTS IX_Sessions_UserId ON Sessions(UserId);
        CREATE INDEX IF NOT EXISTS IX_Sessions_ExpiresAtUtc ON Sessions(ExpiresAtUtc);

        CREATE TABLE IF NOT EXISTS Permissions (
            PermissionId TEXT PRIMARY KEY,
            UserId TEXT NOT NULL,
            PermissionKey TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(UserId)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS UX_Permissions_UserId_PermissionKey ON Permissions(UserId, PermissionKey);
        CREATE INDEX IF NOT EXISTS IX_Permissions_UserId ON Permissions(UserId);

        CREATE TABLE IF NOT EXISTS AuditLog (
            AuditId TEXT PRIMARY KEY,
            OccurredAtUtc TEXT NOT NULL,
            EventName TEXT NOT NULL,
            ActorId TEXT,
            Target TEXT,
            Result TEXT NOT NULL CHECK (Result IN ('success','failed','denied')),
            CorrelationId TEXT,
            DetailsJson TEXT
        );
        CREATE INDEX IF NOT EXISTS IX_AuditLog_OccurredAtUtc ON AuditLog(OccurredAtUtc);
        CREATE INDEX IF NOT EXISTS IX_AuditLog_EventName ON AuditLog(EventName);
        CREATE INDEX IF NOT EXISTS IX_AuditLog_CorrelationId ON AuditLog(CorrelationId);

        CREATE TABLE IF NOT EXISTS ConsumedRecoveryNonces (
            NonceHash TEXT PRIMARY KEY,
            ConsumedAtUtc TEXT NOT NULL,
            RequestId TEXT
        );
        CREATE INDEX IF NOT EXISTS IX_ConsumedRecoveryNonces_RequestId ON ConsumedRecoveryNonces(RequestId);
        CREATE INDEX IF NOT EXISTS IX_ConsumedRecoveryNonces_ConsumedAtUtc ON ConsumedRecoveryNonces(ConsumedAtUtc);

        CREATE TABLE IF NOT EXISTS CustomerDeploymentContext (
            ContextId TEXT PRIMARY KEY,
            CustomerId TEXT NOT NULL,
            DeploymentId TEXT NOT NULL,
            Source TEXT,
            ProtectionScope TEXT,
            SchemaVersion INTEGER NOT NULL CHECK (SchemaVersion >= 1),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS UX_CustomerDeploymentContext_CustomerDeployment ON CustomerDeploymentContext(CustomerId, DeploymentId);

        CREATE TABLE IF NOT EXISTS HostConfiguration (
            ConfigKey TEXT PRIMARY KEY,
            ConfigValue TEXT,
            ValueKind TEXT,
            ProtectionScope TEXT,
            Source TEXT,
            UpdatedAtUtc TEXT
        );
        CREATE INDEX IF NOT EXISTS IX_HostConfiguration_Source ON HostConfiguration(Source);

        CREATE TABLE IF NOT EXISTS StorageMetadata (
            MigrationId TEXT PRIMARY KEY,
            AppliedAtUtc TEXT,
            ChecksumSha256 TEXT NOT NULL,
            AppliedByVersion TEXT
        );
        CREATE INDEX IF NOT EXISTS IX_StorageMetadata_AppliedAtUtc ON StorageMetadata(AppliedAtUtc);

        CREATE TABLE IF NOT EXISTS DeviceIdentity (
            IdentityId TEXT PRIMARY KEY,
            ProtectedStableDeviceIdBlob BLOB NOT NULL,
            AlgorithmVersion TEXT NOT NULL,
            ProtectionVersion TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            RedactedSupportPrefix TEXT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS UX_DeviceIdentity_RedactedSupportPrefix ON DeviceIdentity(RedactedSupportPrefix) WHERE RedactedSupportPrefix IS NOT NULL;
        """;
}
