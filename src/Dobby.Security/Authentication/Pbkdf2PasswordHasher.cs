using System.Security.Cryptography;

namespace Dobby.Security.Authentication;

public sealed record KdfPolicy(
    string Algorithm,
    int Iterations,
    int SaltBytes,
    int HashBytes)
{
    public const string Pbkdf2Sha256Mode = "Pbkdf2Sha256";
    public const string Pbkdf2Sha256Algorithm = "PBKDF2-HMAC-SHA256";
    public const int MinimumVerifiablePbkdf2Iterations = 100_000;
    public const int MaximumVerifiablePbkdf2Iterations = 5_000_000;

    public static KdfPolicy Pbkdf2Sha256V1 { get; } = new(
        Pbkdf2Sha256Algorithm,
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
            expected.Iterations < KdfPolicy.MinimumVerifiablePbkdf2Iterations ||
            expected.Iterations > KdfPolicy.MaximumVerifiablePbkdf2Iterations ||
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
