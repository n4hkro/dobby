using System.Security.Cryptography;
using System.Text;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public sealed record LoginThrottleState(
    bool IsThrottled,
    int RetryAfterSeconds,
    bool IsAvailable = true);

public sealed record LoginThrottlePolicy(
    IReadOnlyList<int> AccountBackoffStepsSeconds,
    int FailureWindowSeconds,
    int DeviceFailureThreshold,
    int DeviceBackoffSeconds);

public sealed record LoginThrottleBucket(
    string BucketKey,
    string BucketKind,
    int FailedAttempts,
    DateTimeOffset WindowStartedAtUtc,
    DateTimeOffset? ThrottledUntilUtc,
    DateTimeOffset UpdatedAtUtc,
    long Version);

public interface ILoginThrottleBucketSession
{
    LoginThrottleBucket? Load(string bucketKey);

    void Upsert(LoginThrottleBucket bucket);

    void Delete(string bucketKey);
}

public interface ILoginThrottleStore
{
    T ExecuteAtomically<T>(Func<ILoginThrottleBucketSession, T> action);
}

public sealed class LoginThrottleService
{
    private const string DeviceBucketKey = "device:local";
    private readonly LoginThrottlePolicy policy;
    private readonly TimeProvider timeProvider;
    private readonly ILoginThrottleStore store;
    private string? lastAccountKey;

    public LoginThrottleService(
        IReadOnlyList<int> stepsSeconds,
        TimeProvider timeProvider,
        ILoginThrottleStore? store = null,
        int failureWindowSeconds = 900,
        int deviceFailureThreshold = 10,
        int deviceBackoffSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(stepsSeconds);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var steps = stepsSeconds.Count == 0 ? [0, 0, 10, 30, 60] : stepsSeconds.ToArray();
        if (steps.Length == 0 || steps.Any(value => value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(stepsSeconds));
        }

        if (failureWindowSeconds <= 0 || deviceFailureThreshold <= 0 || deviceBackoffSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failureWindowSeconds));
        }

        policy = new LoginThrottlePolicy(
            steps,
            failureWindowSeconds,
            deviceFailureThreshold,
            deviceBackoffSeconds);
        this.timeProvider = timeProvider;
        this.store = store ?? new InMemoryLoginThrottleStore();
    }

    public LoginThrottleState Check(string? accountKey = null)
    {
        var effectiveAccountKey = string.IsNullOrWhiteSpace(accountKey)
            ? Volatile.Read(ref lastAccountKey)
            : AccountBucketKey(accountKey);
        return ExecuteSafely(session => ResolveState(session, effectiveAccountKey, timeProvider.GetUtcNow()));
    }

    public LoginThrottleState RecordFailure(string? accountKey)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            return LoginThrottleStateUnavailable();
        }

        var bucketKey = AccountBucketKey(accountKey);
        Volatile.Write(ref lastAccountKey, bucketKey);
        return ExecuteSafely(session =>
        {
            var now = timeProvider.GetUtcNow();
            var account = AdvanceAccountBucket(session.Load(bucketKey), bucketKey, now);
            var device = AdvanceDeviceBucket(session.Load(DeviceBucketKey), now);
            session.Upsert(account);
            session.Upsert(device);
            return ResolveState(account, device, now);
        });
    }

    public LoginThrottleState RecordSuccess(string? accountKey = null)
    {
        var bucketKey = string.IsNullOrWhiteSpace(accountKey)
            ? Volatile.Read(ref lastAccountKey)
            : AccountBucketKey(accountKey);
        if (bucketKey is null)
        {
            return Check();
        }

        var result = ExecuteSafely(session =>
        {
            session.Delete(bucketKey);
            return ResolveState(session, accountBucketKey: null, timeProvider.GetUtcNow());
        });
        if (result.IsAvailable)
        {
            Interlocked.CompareExchange(ref lastAccountKey, null, bucketKey);
        }

        return result;
    }

    private LoginThrottleState ExecuteSafely(Func<ILoginThrottleBucketSession, LoginThrottleState> action)
    {
        try
        {
            return store.ExecuteAtomically(action);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return LoginThrottleStateUnavailable();
        }
    }

    private LoginThrottleBucket AdvanceAccountBucket(
        LoginThrottleBucket? existing,
        string bucketKey,
        DateTimeOffset now)
    {
        var current = ResetExpiredWindow(existing, bucketKey, "account", now);
        var failedAttempts = checked(current.FailedAttempts + 1);
        var delay = policy.AccountBackoffStepsSeconds[
            Math.Min(failedAttempts - 1, policy.AccountBackoffStepsSeconds.Count - 1)];
        return current with
        {
            FailedAttempts = failedAttempts,
            ThrottledUntilUtc = delay == 0 ? null : now.AddSeconds(delay),
            UpdatedAtUtc = now,
            Version = current.Version + 1
        };
    }

    private LoginThrottleBucket AdvanceDeviceBucket(LoginThrottleBucket? existing, DateTimeOffset now)
    {
        var current = ResetExpiredWindow(existing, DeviceBucketKey, "device", now);
        var failedAttempts = checked(current.FailedAttempts + 1);
        var delay = failedAttempts >= policy.DeviceFailureThreshold
            ? policy.DeviceBackoffSeconds
            : 0;
        return current with
        {
            FailedAttempts = failedAttempts,
            ThrottledUntilUtc = delay == 0 ? null : now.AddSeconds(delay),
            UpdatedAtUtc = now,
            Version = current.Version + 1
        };
    }

    private LoginThrottleBucket ResetExpiredWindow(
        LoginThrottleBucket? existing,
        string bucketKey,
        string bucketKind,
        DateTimeOffset now)
    {
        if (existing is null || now >= existing.WindowStartedAtUtc.AddSeconds(policy.FailureWindowSeconds))
        {
            return new LoginThrottleBucket(
                bucketKey,
                bucketKind,
                FailedAttempts: 0,
                WindowStartedAtUtc: now,
                ThrottledUntilUtc: null,
                UpdatedAtUtc: now,
                Version: 0);
        }

        return existing;
    }

    private LoginThrottleState ResolveState(
        ILoginThrottleBucketSession session,
        string? accountBucketKey,
        DateTimeOffset now)
    {
        return ResolveState(
            accountBucketKey is null ? null : session.Load(accountBucketKey),
            session.Load(DeviceBucketKey),
            now);
    }

    private static LoginThrottleState ResolveState(
        LoginThrottleBucket? account,
        LoginThrottleBucket? device,
        DateTimeOffset now)
    {
        var accountSeconds = RemainingSeconds(account?.ThrottledUntilUtc, now);
        var deviceSeconds = RemainingSeconds(device?.ThrottledUntilUtc, now);
        var remaining = Math.Max(accountSeconds, deviceSeconds);
        return new LoginThrottleState(remaining > 0, remaining);
    }

    private static int RemainingSeconds(DateTimeOffset? throttledUntil, DateTimeOffset now)
    {
        var remaining = throttledUntil.GetValueOrDefault() - now;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private static string AccountBucketKey(string accountKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("dobby-login-account-v1\0" + accountKey));
        return "account:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static LoginThrottleState LoginThrottleStateUnavailable()
    {
        return new LoginThrottleState(IsThrottled: true, RetryAfterSeconds: 1, IsAvailable: false);
    }
}

public sealed class InMemoryLoginThrottleStore : ILoginThrottleStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, LoginThrottleBucket> buckets = new(StringComparer.Ordinal);

    public T ExecuteAtomically<T>(Func<ILoginThrottleBucketSession, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (gate)
        {
            return action(new Session(buckets));
        }
    }

    private sealed class Session(Dictionary<string, LoginThrottleBucket> buckets)
        : ILoginThrottleBucketSession
    {
        public LoginThrottleBucket? Load(string bucketKey)
        {
            return buckets.GetValueOrDefault(bucketKey);
        }

        public void Upsert(LoginThrottleBucket bucket)
        {
            buckets[bucket.BucketKey] = bucket;
        }

        public void Delete(string bucketKey)
        {
            buckets.Remove(bucketKey);
        }
    }
}

public sealed class HostLoginThrottleStore(IHostPersistenceTransactionScope transactionScope)
    : ILoginThrottleStore
{
    public T ExecuteAtomically<T>(Func<ILoginThrottleBucketSession, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        T? result = default;
        var completed = false;
        transactionScope.ExecuteInTransaction(transactionExecutor =>
        {
            result = action(new Session(transactionExecutor));
            completed = true;
        });

        return completed
            ? result!
            : throw new InvalidOperationException("Login throttle transaction did not complete.");
    }

    private sealed class Session(IHostPersistenceCommandExecutor executor)
        : ILoginThrottleBucketSession
    {
        public LoginThrottleBucket? Load(string bucketKey)
        {
            var row = executor.Query<BucketRow>(
                "SELECT BucketKey, BucketKind, FailedAttempts, WindowStartedAtUtc, ThrottledUntilUtc, UpdatedAtUtc, Version FROM LoginThrottleBuckets WHERE BucketKey = @BucketKey;",
                new { BucketKey = bucketKey }).SingleOrDefault();
            return row is null
                ? null
                : new LoginThrottleBucket(
                    row.BucketKey,
                    row.BucketKind,
                    row.FailedAttempts,
                    DateTimeOffset.Parse(row.WindowStartedAtUtc, System.Globalization.CultureInfo.InvariantCulture),
                    string.IsNullOrWhiteSpace(row.ThrottledUntilUtc)
                        ? null
                        : DateTimeOffset.Parse(row.ThrottledUntilUtc, System.Globalization.CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(row.UpdatedAtUtc, System.Globalization.CultureInfo.InvariantCulture),
                    row.Version);
        }

        public void Upsert(LoginThrottleBucket bucket)
        {
            executor.Execute(
                """
                INSERT INTO LoginThrottleBuckets(
                    BucketKey, BucketKind, FailedAttempts, WindowStartedAtUtc,
                    ThrottledUntilUtc, UpdatedAtUtc, Version)
                VALUES (
                    @BucketKey, @BucketKind, @FailedAttempts, @WindowStartedAtUtc,
                    @ThrottledUntilUtc, @UpdatedAtUtc, @Version)
                ON CONFLICT(BucketKey) DO UPDATE SET
                    BucketKind = excluded.BucketKind,
                    FailedAttempts = excluded.FailedAttempts,
                    WindowStartedAtUtc = excluded.WindowStartedAtUtc,
                    ThrottledUntilUtc = excluded.ThrottledUntilUtc,
                    UpdatedAtUtc = excluded.UpdatedAtUtc,
                    Version = LoginThrottleBuckets.Version + 1;
                """,
                new
                {
                    bucket.BucketKey,
                    bucket.BucketKind,
                    bucket.FailedAttempts,
                    WindowStartedAtUtc = bucket.WindowStartedAtUtc.ToString("O"),
                    ThrottledUntilUtc = bucket.ThrottledUntilUtc?.ToString("O"),
                    UpdatedAtUtc = bucket.UpdatedAtUtc.ToString("O"),
                    Version = Math.Max(1, bucket.Version)
                });
        }

        public void Delete(string bucketKey)
        {
            executor.Execute(
                "DELETE FROM LoginThrottleBuckets WHERE BucketKey = @BucketKey;",
                new { BucketKey = bucketKey });
        }

        private sealed class BucketRow
        {
            public string BucketKey { get; init; } = string.Empty;

            public string BucketKind { get; init; } = string.Empty;

            public int FailedAttempts { get; init; }

            public string WindowStartedAtUtc { get; init; } = string.Empty;

            public string? ThrottledUntilUtc { get; init; }

            public string UpdatedAtUtc { get; init; } = string.Empty;

            public long Version { get; init; }
        }
    }
}
