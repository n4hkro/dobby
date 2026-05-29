using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.Security.Authentication;

internal static class ProtectedStorageEntropy
{
    public static byte[] ForCredential(byte[]? applicationEntropy, string userId)
    {
        return Derive(applicationEntropy, "Dobby:Sprint5:Credential:v2", userId);
    }

    public static byte[] ForRememberMe(byte[]? applicationEntropy, string slotId, string userId)
    {
        return Derive(applicationEntropy, "Dobby:Sprint5:RememberMe:v2", slotId, userId);
    }

    private static byte[] Derive(byte[]? applicationEntropy, string purpose, params string[] subjects)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, applicationEntropy ?? []);
        Append(hash, Encoding.UTF8.GetBytes(purpose));
        foreach (var subject in subjects)
        {
            Append(hash, Encoding.UTF8.GetBytes(subject));
        }

        return hash.GetHashAndReset();
    }

    private static void Append(IncrementalHash hash, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }
}
