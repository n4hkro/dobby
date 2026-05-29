using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authorization;

public sealed class PermissionGrantStore(IHostPersistenceCommandExecutor executor) : IPermissionGrantReader
{
    public Result<UserPermissionState> LoadUserState(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var row = executor.Query<UserRow>(
            "SELECT UserId, IsOwner, IsActive FROM Users WHERE UserId = @UserId;",
            new { UserId = userId }).SingleOrDefault();

        return row is null
            ? Result.Failure<UserPermissionState>(ErrorCode.NotFound)
            : Result.Success(new UserPermissionState(row.UserId, row.IsOwner == 1, row.IsActive == 1));
    }

    public Result<bool> HasGrant(string userId, OperationKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var count = executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Permissions WHERE UserId = @UserId AND PermissionKey = @PermissionKey;",
            new { UserId = userId, PermissionKey = key.ToString() });
        return Result.Success(count == 1);
    }

    public Result Grant(string userId, OperationKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        executor.Execute(
            "INSERT OR IGNORE INTO Permissions(PermissionId, UserId, PermissionKey, CreatedAtUtc, UpdatedAtUtc) VALUES (@PermissionId, @UserId, @PermissionKey, @CreatedAtUtc, @UpdatedAtUtc);",
            new
            {
                PermissionId = Guid.CreateVersion7().ToString("D"),
                UserId = userId,
                PermissionKey = key.ToString(),
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            });

        return Result.Success();
    }

    public Result Revoke(string userId, OperationKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        executor.Execute(
            "DELETE FROM Permissions WHERE UserId = @UserId AND PermissionKey = @PermissionKey;",
            new { UserId = userId, PermissionKey = key.ToString() });
        return Result.Success();
    }

    public Result<IReadOnlyList<OperationKey>> ListGrants(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var keys = new List<OperationKey>();
        foreach (var permissionKey in executor.Query<string>(
            "SELECT PermissionKey FROM Permissions WHERE UserId = @UserId ORDER BY PermissionKey;",
            new { UserId = userId }))
        {
            var parsed = OperationKey.Parse(permissionKey);
            if (!parsed.IsSuccess)
            {
                return Result.Failure<IReadOnlyList<OperationKey>>(ErrorCode.InvalidInput);
            }

            keys.Add(parsed.Value);
        }

        return Result.Success<IReadOnlyList<OperationKey>>(keys);
    }

    private sealed class UserRow
    {
        public string UserId { get; init; } = string.Empty;

        public int IsOwner { get; init; }

        public int IsActive { get; init; }
    }
}
