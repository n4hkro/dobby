using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public sealed record RememberedLoginAccount(
    string UserId,
    string UsernameNormalized);

public sealed class RememberMeSlotService(IHostPersistenceCommandExecutor executor)
{
    private const string SlotId = "single";
    private const string UsernameOnlyVersion = "username-only-v1";

    public Result ApplySuccessfulLogin(string userId, string usernameNormalized, bool rememberMe)
    {
        return ApplySuccessfulLogin(executor, userId, usernameNormalized, rememberMe);
    }

    public Result ApplySuccessfulLogin(
        IHostPersistenceCommandExecutor commands,
        string userId,
        string usernameNormalized,
        bool rememberMe)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (!rememberMe)
        {
            return ClearSlot(commands);
        }

        var normalized = UsernameCanonicalValidator.Normalize(usernameNormalized);
        if (!normalized.IsSuccess)
        {
            return Result.Failure(normalized.ErrorCode!.Value);
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        commands.Execute(
            """
            INSERT INTO RememberMeSlot(
                SlotId,
                UserId,
                UsernameNormalized,
                Version,
                CreatedAtUtc,
                UpdatedAtUtc)
            VALUES (
                @SlotId,
                @UserId,
                @UsernameNormalized,
                @Version,
                @CreatedAtUtc,
                @UpdatedAtUtc)
            ON CONFLICT(SlotId) DO UPDATE SET
                UserId = excluded.UserId,
                UsernameNormalized = excluded.UsernameNormalized,
                Version = excluded.Version,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """,
            new
            {
                SlotId,
                UserId = userId,
                UsernameNormalized = normalized.Value,
                Version = UsernameOnlyVersion,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

        return Result.Success();
    }

    public Result<RememberedLoginAccount?> LoadRememberedAccount()
    {
        var row = executor.Query<RememberMeRow>(
            """
            SELECT
                slot.UserId,
                slot.UsernameNormalized,
                users.UsernameNormalized AS CurrentUsernameNormalized
            FROM RememberMeSlot AS slot
            LEFT JOIN Users AS users ON users.UserId = slot.UserId
            WHERE slot.SlotId = @SlotId;
            """,
            new { SlotId }).SingleOrDefault();

        if (row is null ||
            string.IsNullOrWhiteSpace(row.UserId) ||
            string.IsNullOrWhiteSpace(row.UsernameNormalized) ||
            string.IsNullOrWhiteSpace(row.CurrentUsernameNormalized))
        {
            ClearSlot();
            return Result.Success<RememberedLoginAccount?>(null);
        }

        var normalized = UsernameCanonicalValidator.Normalize(row.UsernameNormalized);
        if (!normalized.IsSuccess ||
            !StringComparer.Ordinal.Equals(normalized.Value, row.CurrentUsernameNormalized))
        {
            ClearSlot();
            return Result.Success<RememberedLoginAccount?>(null);
        }

        return Result.Success<RememberedLoginAccount?>(
            new RememberedLoginAccount(row.UserId, normalized.Value));
    }

    public Result ClearSlot()
    {
        return ClearSlot(executor);
    }

    public Result ClearSlot(IHostPersistenceCommandExecutor commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        commands.Execute("DELETE FROM RememberMeSlot;");
        return Result.Success();
    }

    private sealed class RememberMeRow
    {
        public string? UserId { get; init; }

        public string? UsernameNormalized { get; init; }

        public string? CurrentUsernameNormalized { get; init; }
    }
}
