using Dobby.Core;

namespace Dobby.Security.Authentication;

public interface IUserCredentialVerifier
{
    Result<bool> VerifyPassword(string userId, string password);
}
