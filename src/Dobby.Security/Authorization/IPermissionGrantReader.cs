using Dobby.Core;

namespace Dobby.Security.Authorization;

public interface IPermissionGrantReader
{
    Result<UserPermissionState> LoadUserState(string userId);

    Result<bool> HasGrant(string userId, OperationKey key);
}
