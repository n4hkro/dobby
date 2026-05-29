namespace Dobby.Security.Authentication;

public static class DisplayNameValidator
{
    public static string? Normalize(string? displayName)
    {
        var trimmed = displayName?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
