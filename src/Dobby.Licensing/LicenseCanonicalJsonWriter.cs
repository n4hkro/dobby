using System.Text;
using System.Text.Json;

namespace Dobby.Licensing;

public static class LicenseCanonicalJsonWriter
{
    public static byte[] Write(JsonElement element)
    {
        var builder = new StringBuilder();
        AppendElement(builder, element);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendElement(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendObject(builder, element);
                break;
            case JsonValueKind.Array:
                AppendArray(builder, element);
                break;
            case JsonValueKind.String:
                AppendString(builder, element.GetString()!.Normalize(NormalizationForm.FormC));
                break;
            case JsonValueKind.Number:
                builder.Append(element.GetRawText());
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static void AppendObject(StringBuilder builder, JsonElement element)
    {
        builder.Append('{');
        var first = true;
        foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            AppendString(builder, property.Name.Normalize(NormalizationForm.FormC));
            builder.Append(':');
            AppendElement(builder, property.Value);
            first = false;
        }

        builder.Append('}');
    }

    private static void AppendArray(StringBuilder builder, JsonElement element)
    {
        builder.Append('[');
        var first = true;
        foreach (var item in element.EnumerateArray())
        {
            if (!first)
            {
                builder.Append(',');
            }

            AppendElement(builder, item);
            first = false;
        }

        builder.Append(']');
    }

    private static void AppendString(StringBuilder builder, string value)
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
