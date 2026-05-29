namespace Dobby.Security.Authentication;

public sealed record ManagedUserDetail(
    string UserId,
    string UsernameNormalized,
    string? DisplayName,
    bool IsOwner,
    bool IsActive,
    IReadOnlyList<string> PermissionKeys)
{
    public string? PasswordHash => null;

    public string? ProtectedCredentialMaterial => null;
}
