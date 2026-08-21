using Dobby.Configuration;
using Dobby.Core;

namespace Dobby.Security.Authentication;

public sealed class KdfPolicyResolver(AuthOptions authOptions)
{
    public Result<KdfPolicy> Resolve()
    {
        return authOptions.PasswordKdfMode switch
        {
            KdfPolicy.Pbkdf2Sha256Mode => Result.Success(KdfPolicy.Pbkdf2Sha256V1),
            _ => Result.Failure<KdfPolicy>(ErrorCode.Unauthorized)
        };
    }
}
