using System.Buffers;
using System.Globalization;
using System.Text;
using Dobby.Core;

namespace Dobby.Security.Authentication;

public static class DisplayNameValidator
{
    public const int MaximumLength = 128;

    public static Result<string?> Normalize(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Success<string?>(null);
        }

        var normalized = trimmed.Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumLength || ContainsUnsafeUnicode(normalized))
        {
            return Result.Failure<string?>(ErrorCode.ValidationFailed);
        }

        return Result.Success<string?>(normalized);
    }

    private static bool ContainsUnsafeUnicode(string value)
    {
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                return true;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or
                UnicodeCategory.Format or
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator)
            {
                return true;
            }

            remaining = remaining[consumed..];
        }

        return false;
    }
}
