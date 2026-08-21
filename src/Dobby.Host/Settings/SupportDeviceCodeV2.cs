using System.Security.Cryptography;
using System.Text;
using Dobby.Security.Authentication;

namespace Dobby.Host.Settings;

public static class SupportDeviceCodeV2
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Compute(
        ReadOnlySpan<byte> stableDeviceId,
        ReadOnlySpan<byte> supportCodeKey)
    {
        if (stableDeviceId.Length != 32 || supportCodeKey.Length != 32)
        {
            return "device-unavailable";
        }

        var deviceId = DeviceRecoveryIdentity.ComputeDeviceId(stableDeviceId);
        var message = Encoding.UTF8.GetBytes("dobby-support-code-v2\0" + deviceId);
        var digest = HMACSHA256.HashData(supportCodeKey, message);
        var symbols = EncodeBase32(digest, 10);
        var checksum = Alphabet[Enumerable.Range(0, symbols.Length)
            .Sum(index => (index + 1) * Alphabet.IndexOf(symbols[index])) % Alphabet.Length];
        return $"D2-{symbols[..5]}-{symbols[5..]}-{checksum}";
    }

    public static bool HasValidChecksum(string? code)
    {
        if (code is not { Length: 16 } ||
            !code.StartsWith("D2-", StringComparison.Ordinal) ||
            code[8] != '-' ||
            code[14] != '-')
        {
            return false;
        }

        var symbols = code.Substring(3, 5) + code.Substring(9, 5);
        if (symbols.Any(character => Alphabet.IndexOf(character) < 0))
        {
            return false;
        }

        var expected = Alphabet[Enumerable.Range(0, symbols.Length)
            .Sum(index => (index + 1) * Alphabet.IndexOf(symbols[index])) % Alphabet.Length];
        return code[^1] == expected;
    }

    private static string EncodeBase32(ReadOnlySpan<byte> bytes, int symbolCount)
    {
        var result = new char[symbolCount];
        var buffer = 0;
        var bitsInBuffer = 0;
        var byteIndex = 0;
        for (var index = 0; index < result.Length; index++)
        {
            while (bitsInBuffer < 5)
            {
                buffer = (buffer << 8) | bytes[byteIndex++];
                bitsInBuffer += 8;
            }

            bitsInBuffer -= 5;
            result[index] = Alphabet[(buffer >> bitsInBuffer) & 31];
        }

        return new string(result);
    }
}
