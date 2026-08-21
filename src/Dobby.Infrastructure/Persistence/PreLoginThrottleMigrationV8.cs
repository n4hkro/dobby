namespace Dobby.Infrastructure.Persistence;

internal static class PreLoginThrottleMigrationV8
{
    public const string MigrationId = "0008_prelogin_login_throttle";

    public const string Sql = """
        CREATE TABLE LoginThrottleBuckets (
            BucketKey TEXT PRIMARY KEY,
            BucketKind TEXT NOT NULL CHECK (BucketKind IN ('account', 'device')),
            FailedAttempts INTEGER NOT NULL CHECK (FailedAttempts BETWEEN 0 AND 100000),
            WindowStartedAtUtc TEXT NOT NULL,
            ThrottledUntilUtc TEXT,
            UpdatedAtUtc TEXT NOT NULL,
            Version INTEGER NOT NULL CHECK (Version >= 1)
        );
        CREATE INDEX IX_LoginThrottleBuckets_UpdatedAtUtc
            ON LoginThrottleBuckets(UpdatedAtUtc);
        """;
}
