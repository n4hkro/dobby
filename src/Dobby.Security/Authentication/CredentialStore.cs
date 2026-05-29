using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using System.Security.Cryptography;

namespace Dobby.Security.Authentication;

public sealed class CredentialStore(
    IHostPersistenceCommandExecutor executor,
    IProtectedDataProtector protector,
    KdfPolicyResolver kdfPolicyResolver,
    byte[]? applicationEntropy = null) : IUserCredentialVerifier
{
    public Result CreateCredential(string userId, string password, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var validation = PasswordPolicyValidator.Validate(password);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        var policy = kdfPolicyResolver.Resolve();
        if (!policy.IsSuccess)
        {
            return Result.Failure(policy.ErrorCode!.Value);
        }

        var hasher = new Pbkdf2PasswordHasher(policy.Value);
        var hash = hasher.Hash(password);
        var timestamp = now.ToUniversalTime().ToString("O");
        var entropy = ProtectedStorageEntropy.ForCredential(applicationEntropy, userId);

        executor.Execute(
            "INSERT INTO Credentials(CredentialId, UserId, KdfMode, KdfVersion, PasswordHash, Salt, SaltBitLength, CreatedAtUtc, UpdatedAtUtc, PasswordChangedAtUtc) VALUES (@CredentialId, @UserId, @KdfMode, @KdfVersion, @PasswordHash, @Salt, @SaltBitLength, @CreatedAtUtc, @UpdatedAtUtc, NULL);",
            new
            {
                CredentialId = Guid.CreateVersion7().ToString("D"),
                UserId = userId,
                KdfMode = "Pbkdf2Sha256",
                KdfVersion = $"{hash.Algorithm}:{hash.Iterations}",
                PasswordHash = protector.Protect(hash.Hash, entropy),
                Salt = protector.Protect(hash.Salt, entropy),
                SaltBitLength = hash.Salt.Length * 8,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });

        return Result.Success();
    }

    public Result<bool> VerifyPassword(string userId, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

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
        if (!StringComparer.Ordinal.Equals(row.KdfMode, "Pbkdf2Sha256") ||
            persistedPolicy is null ||
            !StringComparer.Ordinal.Equals(persistedPolicy.Value.Algorithm, policy.Value.Algorithm) ||
            persistedPolicy.Value.Iterations < policy.Value.Iterations)
        {
            return Result.Failure<bool>(ErrorCode.Unauthorized);
        }

        var salt = UnprotectOrEmpty(row.Salt, userId, out var saltUnwrapped);
        var passwordHash = UnprotectOrEmpty(row.PasswordHash, userId, out var hashUnwrapped);
        if (!saltUnwrapped ||
            !hashUnwrapped ||
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
        return Result.Success(new Pbkdf2PasswordHasher(policy.Value).Verify(password, hash));
    }

    public Result RebindPassword(string userId, string password, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var validation = PasswordPolicyValidator.Validate(password);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        var policy = kdfPolicyResolver.Resolve();
        if (!policy.IsSuccess)
        {
            return Result.Failure(policy.ErrorCode!.Value);
        }

        var hasher = new Pbkdf2PasswordHasher(policy.Value);
        var hash = hasher.Hash(password);
        var timestamp = now.ToUniversalTime().ToString("O");
        var entropy = ProtectedStorageEntropy.ForCredential(applicationEntropy, userId);
        var affected = executor.Execute(
            "UPDATE Credentials SET KdfMode = @KdfMode, KdfVersion = @KdfVersion, PasswordHash = @PasswordHash, Salt = @Salt, SaltBitLength = @SaltBitLength, UpdatedAtUtc = @UpdatedAtUtc, PasswordChangedAtUtc = @PasswordChangedAtUtc WHERE UserId = @UserId;",
            new
            {
                UserId = userId,
                KdfMode = "Pbkdf2Sha256",
                KdfVersion = $"{hash.Algorithm}:{hash.Iterations}",
                PasswordHash = protector.Protect(hash.Hash, entropy),
                Salt = protector.Protect(hash.Salt, entropy),
                SaltBitLength = hash.Salt.Length * 8,
                UpdatedAtUtc = timestamp,
                PasswordChangedAtUtc = timestamp
            });

        return affected == 0 ? Result.Failure(ErrorCode.NotFound) : Result.Success();
    }

    private CredentialRow? LoadCredential(string userId)
    {
        return executor.Query<CredentialRow>(
            "SELECT KdfMode, KdfVersion, PasswordHash, Salt FROM Credentials WHERE UserId = @UserId;",
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
        return separator > 0 && int.TryParse(kdfVersion![(separator + 1)..], out var iterations)
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
    }
}
