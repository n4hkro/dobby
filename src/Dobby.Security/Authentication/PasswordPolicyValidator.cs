using Dobby.Core;

namespace Dobby.Security.Authentication;

public static class PasswordPolicyValidator
{
    public static Result Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > 128)
        {
            return Result.Failure(ErrorCode.ValidationFailed);
        }

        return Result.Success();
    }
}
