using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;

namespace Dobby.Security.Authentication;

public sealed class OwnerBootstrapService(
    IProtectedDataProtector credentialProtector,
    KdfPolicyResolver kdfPolicyResolver,
    byte[]? applicationEntropy = null)
{
    public Result<string> BootstrapOwner(
        IHostPersistenceCommandExecutor executor,
        string username,
        string? displayName,
        string password,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(executor);

        if (executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") > 0)
        {
            return Result.Failure<string>(ErrorCode.AlreadyExists);
        }

        var normalizedUsername = UsernameCanonicalValidator.Normalize(username);
        if (!normalizedUsername.IsSuccess)
        {
            return Result.Failure<string>(normalizedUsername.ErrorCode!.Value);
        }

        var passwordValidation = PasswordPolicyValidator.Validate(password);
        if (!passwordValidation.IsSuccess)
        {
            return Result.Failure<string>(passwordValidation.ErrorCode!.Value);
        }

        var normalizedDisplayName = DisplayNameValidator.Normalize(displayName);
        if (!normalizedDisplayName.IsSuccess)
        {
            return Result.Failure<string>(normalizedDisplayName.ErrorCode!.Value);
        }

        var userId = Guid.CreateVersion7().ToString("D");
        var timestamp = now.ToUniversalTime().ToString("O");
        executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, @UsernameNormalized, @DisplayName, 1, 1, @CreatedAtUtc, @UpdatedAtUtc);",
            new
            {
                UserId = userId,
                UsernameNormalized = normalizedUsername.Value,
                DisplayName = normalizedDisplayName.Value,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });

        var credentials = new CredentialStore(executor, credentialProtector, kdfPolicyResolver, applicationEntropy);
        var credentialResult = credentials.CreateCredential(userId, password, now);
        if (!credentialResult.IsSuccess)
        {
            return Result.Failure<string>(credentialResult.ErrorCode!.Value);
        }

        return Result.Success(userId);
    }
}
