using Dobby.Core;

namespace Dobby.Security.Authentication;

public static class UsernameCanonicalValidator
{
    public static Result<string> Normalize(string? username, IReadOnlySet<string>? existingNormalizedUsernames = null)
    {
        var normalized = username?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length < 3 || normalized.Length > 64)
        {
            return Result.Failure<string>(ErrorCode.ValidationFailed);
        }

        foreach (var character in normalized)
        {
            if (!IsAllowed(character))
            {
                return Result.Failure<string>(ErrorCode.ValidationFailed);
            }
        }

        if (existingNormalizedUsernames is not null && existingNormalizedUsernames.Contains(normalized))
        {
            return Result.Failure<string>(ErrorCode.AlreadyExists);
        }

        return Result.Success(normalized);
    }

    private static bool IsAllowed(char character)
    {
        return character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character is '_' or '-' or '.';
    }
}
