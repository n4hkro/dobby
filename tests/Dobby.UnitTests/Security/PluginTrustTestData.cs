using System.Security.Cryptography;
using System.Text;

namespace Dobby.UnitTests.Security;

internal static class PluginTrustTestData
{
    public static readonly byte[] CanonicalPayload = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");

    public static ECDsa CreateKeyPair(out byte[] publicKeyDer, out byte[] publicKeyPem)
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        publicKeyDer = key.ExportSubjectPublicKeyInfo();
        publicKeyPem = EncodePem(publicKeyDer);
        return key;
    }

    public static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashHex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static byte[] EncodePem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        var pem = $"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----\n";
        return Encoding.UTF8.GetBytes(pem);
    }
}
