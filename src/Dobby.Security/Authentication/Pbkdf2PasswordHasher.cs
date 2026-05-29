using System.Security.Cryptography;

namespace Dobby.Security.Authentication;

public sealed record KdfPolicy(
    string Algorithm,
    int Iterations,
    int SaltBytes,
    int HashBytes)
{
    public static KdfPolicy SkeletonPbkdf2Fallback { get; } = new(
        "PBKDF2-HMAC-SHA256",
        600_000,
        16,
        32);
}

public sealed record PasswordHash(
    string Algorithm,
    int Iterations,
    byte[] Salt,
    byte[] Hash);

public sealed class Pbkdf2PasswordHasher(KdfPolicy policy)
{
    public PasswordHash Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(policy.SaltBytes);
        var hash = Derive(password, salt);
        return new PasswordHash(policy.Algorithm, policy.Iterations, salt, hash);
    }

    public bool Verify(string password, PasswordHash expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        if (!StringComparer.Ordinal.Equals(expected.Algorithm, policy.Algorithm) ||
            expected.Iterations < policy.Iterations ||
            expected.Salt.Length < policy.SaltBytes ||
            expected.Hash.Length < policy.HashBytes)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            expected.Salt,
            expected.Iterations,
            HashAlgorithmName.SHA256,
            expected.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected.Hash);
    }

    private byte[] Derive(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            policy.Iterations,
            HashAlgorithmName.SHA256,
            policy.HashBytes);
    }
}
