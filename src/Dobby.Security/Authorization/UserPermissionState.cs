namespace Dobby.Security.Authorization;

public sealed record UserPermissionState(string UserId, bool IsOwner, bool IsActive);
