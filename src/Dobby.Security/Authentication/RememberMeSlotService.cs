using System.Text;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;

namespace Dobby.Security.Authentication;

public sealed record RememberMeSavedLogin(
    string UserId,
    string MaskedUsername,
    string Password,
    bool HasMaskedPasswordPrefill);

public sealed class RememberMeSlotService(
    IHostPersistenceCommandExecutor executor,
    IProtectedDataProtector protector,
    byte[]? applicationEntropy = null,
    IRememberMeStartupSnapshotSink? startupSnapshotSink = null)
{
    private const string SlotId = "single";
    private static readonly byte[] PasswordPayloadPrefix = "Dobby:RememberMePassword:v1\0"u8.ToArray();
    private readonly IRememberMeStartupSnapshotSink startupSnapshotSinkCore =
        startupSnapshotSink ?? NullRememberMeStartupSnapshotSink.Instance;

    public Result ApplySuccessfulLogin(string userId, string usernameNormalized, string password, bool rememberMe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameNormalized);
        ArgumentNullException.ThrowIfNull(password);

        if (!rememberMe)
        {
            return ClearSlot();
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var payload = PasswordPayloadPrefix
            .Concat(Encoding.UTF8.GetBytes(password))
            .ToArray();
        var protectedBlob = protector.Protect(
            payload,
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
                ProtectionVersion = "dpapi-current-user-v2",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        SaveStartupSnapshot(new RememberMeSavedLogin(
            userId,
            usernameNormalized,
            password,
            true));

        return Result.Success();
    }

    public Result<RememberMeSavedLogin?> LoadSavedLogin()
    {
        var row = executor.Query<RememberMeRow>(
            "SELECT UserId, MaskedUsername, ProtectedRememberMeBlob FROM RememberMeSlot WHERE SlotId = @SlotId;",
            new { SlotId }).SingleOrDefault();
        if (row is null || row.ProtectedRememberMeBlob is null || row.UserId is null || row.MaskedUsername is null)
        {
            ClearStartupSnapshot();
            return Result.Success<RememberMeSavedLogin?>(null);
        }

        try
        {
            var plaintext = protector.Unprotect(
                row.ProtectedRememberMeBlob,
                ProtectedStorageEntropy.ForRememberMe(applicationEntropy, SlotId, row.UserId));
            if (!plaintext.AsSpan().StartsWith(PasswordPayloadPrefix))
            {
                ClearSlot();
                return Result.Success<RememberMeSavedLogin?>(null);
            }

            var savedCredential = Encoding.UTF8.GetString(plaintext.AsSpan(PasswordPayloadPrefix.Length));
            var savedLogin = new RememberMeSavedLogin(
                row.UserId,
                row.MaskedUsername,
                savedCredential,
                true);
            SaveStartupSnapshot(savedLogin);
            return Result.Success<RememberMeSavedLogin?>(savedLogin);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            ClearSlot();
            return Result.Success<RememberMeSavedLogin?>(null);
        }
    }

    public Result ClearSlot()
    {
        executor.Execute("DELETE FROM RememberMeSlot;");
        ClearStartupSnapshot();
        return Result.Success();
    }

    private void SaveStartupSnapshot(RememberMeSavedLogin savedLogin)
    {
        try
        {
            startupSnapshotSinkCore.Save(savedLogin);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    private void ClearStartupSnapshot()
    {
        try
        {
            startupSnapshotSinkCore.Clear();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }

    private sealed class RememberMeRow
    {
        public string? UserId { get; init; }

        public string? MaskedUsername { get; init; }

        public byte[]? ProtectedRememberMeBlob { get; init; }
    }
}
