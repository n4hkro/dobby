using System.Text;

namespace Dobby.Security.Authentication;

public static class RecoveryCanonicalJsonWriter
{
    public static byte[] WritePayload(RecoveryResponsePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var builder = new StringBuilder();
        builder.Append('{');
        AppendPropertyName(builder, "deviceIdentity");
        builder.Append('{');
        AppendStringProperty(builder, "cpuId", payload.DeviceIdentity.CpuId, first: true);
        AppendStringProperty(builder, "diskSerial", payload.DeviceIdentity.DiskSerial);
        AppendStringProperty(builder, "macAddress", payload.DeviceIdentity.MacAddress);
        AppendStringProperty(builder, "machineGuid", payload.DeviceIdentity.MachineGuid);
        AppendStringProperty(builder, "motherboardSerial", payload.DeviceIdentity.MotherboardSerial);
        builder.Append('}');
        AppendStringProperty(builder, "expiresAt", payload.ExpiresAt);
        AppendStringProperty(builder, "issuedAt", payload.IssuedAt);
        builder.Append(',');
        AppendPropertyName(builder, "keyVersion");
        builder.Append(payload.KeyVersion);
        AppendStringProperty(builder, "nonce", payload.Nonce);
        AppendStringProperty(builder, "requestId", payload.RequestId);
        AppendStringProperty(builder, "type", payload.Type);
        builder.Append('}');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendStringProperty(StringBuilder builder, string name, string value, bool first = false)
    {
        if (!first)
        {
            builder.Append(',');
        }

        AppendPropertyName(builder, name);
        AppendStringValue(builder, value);
    }

    private static void AppendPropertyName(StringBuilder builder, string name)
    {
        AppendStringValue(builder, name);
        builder.Append(':');
    }

    private static void AppendStringValue(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\u{(int)character:x4}",
                _ => character
            });
        }

        builder.Append('"');
    }
}
