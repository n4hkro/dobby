using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using System.Security.Cryptography;

namespace Dobby.Security.Authentication;

public enum CredentialMutationKind
{
    Create,
    Rebind
}

public sealed class PreparedCredentialMutation
{
    internal PreparedCredentialMutation(
        CredentialMutationKind kind,
        string userId,
        string kdfVersion,
        byte[] protectedPasswordHash,
        byte[] protectedSalt,
        int saltBitLength,
        string timestamp)
    {
        Kind = kind;
        UserId = userId;
        KdfVersion = kdfVersion;
        ProtectedPasswordHash = protectedPasswordHash;
        ProtectedSalt = protectedSalt;
        SaltBitLength = saltBitLength;
        Timestamp = timestamp;
    }

    internal CredentialMutationKind Kind { get; }

    internal string UserId { get; }

    internal string KdfVersion { get; }

    internal byte[] ProtectedPasswordHash { get; }

    internal byte[] ProtectedSalt { get; }

    internal int SaltBitLength { get; }

    internal string Timestamp { get; }
}

public sealed class CredentialStore(
    IHostPersistenceCommandExecutor executor,
    IProtectedDataProtector protector,
    KdfPolicyResolver kdfPolicyResolver,
    byte[]? applicationEntropy = null,
    TimeProvider? timeProvider = null,
    CredentialVerificationGate? verificationGate = null) : IUserCredentialVerifier
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly CredentialVerificationGate verifierGate = verificationGate ?? new(2);

    public Result CreateCredential(string userId, string password, DateTimeOffset now)
    {
        var prepared = PrepareCreateCredential(userId, password, now);
        return prepared.IsSuccess
            ? ApplyPreparedCredential(executor, prepared.Value)
            : Result.Failure(prepared.ErrorCode!.Value);
    }

    public Result<bool> VerifyPassword(string userId, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using var verifierLease = verifierGate.TryAcquire();
        if (verifierLease is null)
        {
            return Result.Failure<bool>(ErrorCode.ResourceBusy);
        }

        var row = LoadCredential(userId);
        if (row is null)
        {
            return Result.Failure<bool>(ErrorCode.NotFound);
        }

        var policy = kdfPolicyResolver.Resolve();
        if (!policy.IsSuccess)
        {
            return Result.Failure<bool>(policy.ErrorCode!.Value);
        }

        var persistedPolicy = ParseKdfVersion(row.KdfVersion);
        if (!StringComparer.Ordinal.Equals(row.KdfMode, KdfPolicy.Pbkdf2Sha256Mode) ||
            persistedPolicy is null ||
            !StringComparer.Ordinal.Equals(persistedPolicy.Value.Algorithm, policy.Value.Algorithm) ||
            persistedPolicy.Value.Iterations < KdfPolicy.MinimumVerifiablePbkdf2Iterations ||
            persistedPolicy.Value.Iterations > KdfPolicy.MaximumVerifiablePbkdf2Iterations)
        {
            return Result.Failure<bool>(ErrorCode.Unauthorized);
        }

        var salt = UnprotectOrEmpty(row.Salt, userId, out var saltUnwrapped);
        var passwordHash = UnprotectOrEmpty(row.PasswordHash, userId, out var hashUnwrapped);
        if (!saltUnwrapped ||
            !hashUnwrapped ||
            row.SaltBitLength != salt.Length * 8 ||
            salt.Length < policy.Value.SaltBytes ||
            passwordHash.Length < policy.Value.HashBytes)
        {
            return Result.Failure<bool>(ErrorCode.Unauthorized);
        }

        var hash = new PasswordHash(
            persistedPolicy.Value.Algorithm,
            persistedPolicy.Value.Iterations,
            salt,
            passwordHash);
        var verified = new Pbkdf2PasswordHasher(policy.Value).Verify(password, hash);
        if (!verified)
        {
            return Result.Success(false);
        }

        if (persistedPolicy.Value.Iterations < policy.Value.Iterations ||
            salt.Length < policy.Value.SaltBytes ||
            passwordHash.Length < policy.Value.HashBytes)
        {
            var upgrade = RebindPassword(userId, password, clock.GetUtcNow());
            if (!upgrade.IsSuccess)
            {
                return Result.Failure<bool>(upgrade.ErrorCode!.Value);
            }
        }

        return Result.Success(true);
    }

    public Result RebindPassword(string userId, string password, DateTimeOffset now)
    {
        var prepared = PreparePasswordRebind(userId, password, now);
        return prepared.IsSuccess
            ? ApplyPreparedCredential(executor, prepared.Value)
            : Result.Failure(prepared.ErrorCode!.Value);
    }

    public Result<PreparedCredentialMutation> PrepareCreateCredential(
        string userId,
        string password,
        DateTimeOffset now)
    {
        return PrepareCredential(CredentialMutationKind.Create, userId, password, now);
    }

    public Result<PreparedCredentialMutation> PreparePasswordRebind(
        string userId,
        string password,
        DateTimeOffset now)
    {
        return PrepareCredential(CredentialMutationKind.Rebind, userId, password, now);
    }

    public Result ApplyPreparedCredential(
        IHostPersistenceCommandExecutor commands,
        PreparedCredentialMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(mutation);

        if (mutation.Kind == CredentialMutationKind.Create)
        {
            commands.Execute(
                "INSERT INTO Credentials(CredentialId, UserId, KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength, CreatedAtUtc, UpdatedAtUtc, PasswordChangedAtUtc) VALUES (@CredentialId, @UserId, @KdfMode, @KdfVersion, @PasswordHash, @Salt, @SaltBitLength, @CreatedAtUtc, @UpdatedAtUtc, NULL);",
                new
                {
                    CredentialId = Guid.CreateVersion7().ToString("D"),
                    mutation.UserId,
                    KdfMode = KdfPolicy.Pbkdf2Sha256Mode,
                    mutation.KdfVersion,
                    PasswordHash = mutation.ProtectedPasswordHash,
                    Salt = mutation.ProtectedSalt,
                    mutation.SaltBitLength,
                    CreatedAtUtc = mutation.Timestamp,
                    UpdatedAtUtc = mutation.Timestamp
                });
            return Result.Success();
        }

        var affected = commands.Execute(
            "UPDATE Credentials SET KdfMode = @KdfMode, KdfVersion = @KdfVersion, PasswordHash = @PasswordHash, Salt = @Salt, SaltBitLength = @SaltBitLength, UpdatedAtUtc = @UpdatedAtUtc, PasswordChangedAtUtc = @PasswordChangedAtUtc WHERE UserId = @UserId;",
            new
            {
                mutation.UserId,
                KdfMode = KdfPolicy.Pbkdf2Sha256Mode,
                mutation.KdfVersion,
                PasswordHash = mutation.ProtectedPasswordHash,
                Salt = mutation.ProtectedSalt,
                mutation.SaltBitLength,
                UpdatedAtUtc = mutation.Timestamp,
                PasswordChangedAtUtc = mutation.Timestamp
            });

        return affected == 0 ? Result.Failure(ErrorCode.NotFound) : Result.Success();
    }

    private Result<PreparedCredentialMutation> PrepareCredential(
        CredentialMutationKind kind,
        string userId,
        string password,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var validation = PasswordPolicyValidator.Validate(password);
        if (!validation.IsSuccess)
        {
            return Result.Failure<PreparedCredentialMutation>(validation.ErrorCode!.Value);
        }

        var policy = kdfPolicyResolver.Resolve();
        if (!policy.IsSuccess)
        {
            return Result.Failure<PreparedCredentialMutation>(policy.ErrorCode!.Value);
        }

        var hasher = new Pbkdf2PasswordHasher(policy.Value);
        var hash = hasher.Hash(password);
        try
        {
            var entropy = ProtectedStorageEntropy.ForCredential(applicationEntropy, userId);
            return Result.Success(new PreparedCredentialMutation(
                kind,
                userId,
                $"{hash.Algorithm}:{hash.Iterations}",
                protector.Protect(hash.Hash, entropy),
                protector.Protect(hash.Salt, entropy),
                hash.Salt.Length * 8,
                now.ToUniversalTime().ToString("O")));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash.Hash);
            CryptographicOperations.ZeroMemory(hash.Salt);
        }
    }

    private CredentialRow? LoadCredential(string userId)
    {
        return executor.Query<CredentialRow>(
            "SELECT KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength FROM Credentials WHERE UserId = @UserId;",
            new { UserId = userId }).SingleOrDefault();
    }

    private byte[] UnprotectOrEmpty(byte[] protectedBlob, string userId, out bool unwrapped)
    {
        try
        {
            unwrapped = true;
            return protector.Unprotect(protectedBlob, ProtectedStorageEntropy.ForCredential(applicationEntropy, userId));
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            unwrapped = false;
            return [];
        }
    }

    private static PersistedKdfPolicy? ParseKdfVersion(string? kdfVersion)
    {
        var separator = kdfVersion?.LastIndexOf(':') ?? -1;
        return separator > 0 && int.TryParse(
                kdfVersion![(separator + 1)..],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var iterations)
            ? new PersistedKdfPolicy(kdfVersion![..separator], iterations)
            : null;
    }

    private readonly record struct PersistedKdfPolicy(string Algorithm, int Iterations);

    private sealed class CredentialRow
    {
        public string? KdfMode { get; init; }

        public string? KdfVersion { get; init; }

        public byte[] PasswordHash { get; init; } = [];

        public byte[] Salt { get; init; } = [];

        public int SaltBitLength { get; init; }
    }
}
