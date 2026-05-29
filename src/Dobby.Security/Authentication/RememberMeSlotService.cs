using System.Text;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;

namespace Dobby.Security.Authentication;

public sealed record RememberMeSavedLogin(string UserId, string MaskedUsername, bool HasMaskedPasswordPrefill);

public sealed class RememberMeSlotService(
    IHostPersistenceCommandExecutor executor,
    IProtectedDataProtector protector,
    byte[]? applicationEntropy = null)
{
    private const string SlotId = "single";

    public Result ApplySuccessfulLogin(string userId, string usernameNormalized, bool rememberMe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameNormalized);

        if (!rememberMe)
        {
            return ForgetSavedLogin();
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var protectedBlob = protector.Protect(
            Encoding.UTF8.GetBytes(userId),
            ProtectedStorageEntropy.ForRememberMe(applicationEntropy, SlotId, userId));
        executor.Execute("DELETE FROM RememberMeSlot;");
        executor.Execute(
            "INSERT INTO RememberMeSlot(SlotId, UserId, MaskedUsername, ProtectedRememberMeBlob, ProtectionVersion, CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc) VALUES (@SlotId, @UserId, @MaskedUsername, @ProtectedRememberMeBlob, @ProtectionVersion, @CreatedAtUtc, @UpdatedAtUtc, NULL);",
            new
            {
                SlotId,
                UserId = userId,
                MaskedUsername = usernameNormalized,
                ProtectedRememberMeBlob = protectedBlob,
                ProtectionVersion = "dpapi-current-user-v1",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        return Result.Success();
    }

    public Result<RememberMeSavedLogin?> LoadSavedLogin()
    {
        var row = executor.Query<RememberMeRow>(
            "SELECT UserId, MaskedUsername, ProtectedRememberMeBlob FROM RememberMeSlot WHERE SlotId = @SlotId;",
            new { SlotId }).SingleOrDefault();
        if (row is null || row.ProtectedRememberMeBlob is null || row.UserId is null || row.MaskedUsername is null)
        {
            return Result.Success<RememberMeSavedLogin?>(null);
        }

        try
        {
            protector.Unprotect(
                row.ProtectedRememberMeBlob,
                ProtectedStorageEntropy.ForRememberMe(applicationEntropy, SlotId, row.UserId));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            ForgetSavedLogin();
            return Result.Success<RememberMeSavedLogin?>(null);
        }

        return Result.Success<RememberMeSavedLogin?>(new RememberMeSavedLogin(row.UserId, row.MaskedUsername, true));
    }

    public Result ForgetSavedLogin()
    {
        executor.Execute("DELETE FROM RememberMeSlot;");
        return Result.Success();
    }

    private sealed class RememberMeRow
    {
        public string? UserId { get; init; }

        public string? MaskedUsername { get; init; }

        public byte[]? ProtectedRememberMeBlob { get; init; }
    }
}
