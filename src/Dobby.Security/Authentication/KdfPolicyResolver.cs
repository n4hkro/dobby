using Dobby.Configuration;
using Dobby.Core;

namespace Dobby.Security.Authentication;

public sealed class KdfPolicyResolver(AuthOptions authOptions)
{
    public Result<KdfPolicy> Resolve()
    {
        return authOptions.PasswordKdfMode switch
        {
            "Pbkdf2Sha256" => Result.Success(KdfPolicy.SkeletonPbkdf2Fallback),
            _ => Result.Failure<KdfPolicy>(ErrorCode.Unauthorized)
        };
    }
}
