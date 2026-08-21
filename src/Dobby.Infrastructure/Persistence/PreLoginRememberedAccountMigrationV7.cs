namespace Dobby.Infrastructure.Persistence;

internal static class PreLoginRememberedAccountMigrationV7
{
    public const string MigrationId = "0007_prelogin_remembered_account";

    public const string Sql = """
        DROP INDEX IF EXISTS UX_RememberMeSlot_SlotId;
        DROP INDEX IF EXISTS IX_RememberMeSlot_UserId;

        CREATE TABLE RememberMeSlot_UsernameOnly (
            SlotId TEXT PRIMARY KEY CHECK (SlotId = 'single'),
            UserId TEXT NOT NULL,
            UsernameNormalized TEXT NOT NULL CHECK (
                length(UsernameNormalized) BETWEEN 3 AND 64 AND
                UsernameNormalized NOT GLOB '*[^a-z0-9_.-]*'),
            Version TEXT NOT NULL CHECK (Version = 'username-only-v1'),
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(UserId)
        );

        INSERT INTO RememberMeSlot_UsernameOnly(
            SlotId,
            UserId,
            UsernameNormalized,
            Version,
            CreatedAtUtc,
            UpdatedAtUtc)
        SELECT
            legacy.SlotId,
            legacy.UserId,
            legacy.MaskedUsername,
            'username-only-v1',
            COALESCE(legacy.CreatedAtUtc, @MigratedAtUtc),
            @MigratedAtUtc
        FROM RememberMeSlot AS legacy
        INNER JOIN Users AS users
            ON users.UserId = legacy.UserId
            AND users.UsernameNormalized = legacy.MaskedUsername
        WHERE legacy.SlotId = 'single'
          AND length(legacy.MaskedUsername) BETWEEN 3 AND 64
          AND legacy.MaskedUsername NOT GLOB '*[^a-z0-9_.-]*';

        DROP TABLE RememberMeSlot;
        ALTER TABLE RememberMeSlot_UsernameOnly RENAME TO RememberMeSlot;
        CREATE INDEX IX_RememberMeSlot_UserId ON RememberMeSlot(UserId);
        """;
}
