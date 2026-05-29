using Dobby.Core;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class UserManagementAuthorizationGuardTests
{
    [Fact]
    public void Managed_user_cannot_mutate_owner_even_with_grant_permission()
    {
        var grants = GuardGrantReader.CreateManagedWithGrants(
            "actor",
            new OperationKey("users.management", OperationVerb.Grant),
            new OperationKey("users.management", OperationVerb.Delete));
        grants.AddUser(new UserPermissionState("owner", IsOwner: true, IsActive: true));
        var guard = CreateGuard(grants);

        var result = guard.EnsureCanManageUser("actor", "owner", OperationVerb.Delete, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Managed_user_cannot_grant_permission_to_self()
    {
        var grants = GuardGrantReader.CreateManagedWithGrants(
            "actor",
            new OperationKey("users.management", OperationVerb.Grant),
            new OperationKey("users.management", OperationVerb.View));
        var guard = CreateGuard(grants);

        var result = guard.EnsureCanGrant("actor", "actor", new OperationKey("users.management", OperationVerb.View), CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Managed_user_cannot_grant_permission_they_do_not_have()
    {
        var grants = GuardGrantReader.CreateManagedWithGrants("actor", new OperationKey("users.management", OperationVerb.Grant));
        grants.AddUser(new UserPermissionState("target", IsOwner: false, IsActive: true));
        var guard = CreateGuard(grants);

        var result = guard.EnsureCanGrant("actor", "target", new OperationKey("plugins.license", OperationVerb.Import), CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Owner_can_grant_registered_permissions_to_managed_users()
    {
        var grants = new GuardGrantReader(new UserPermissionState("owner", IsOwner: true, IsActive: true));
        grants.AddUser(new UserPermissionState("target", IsOwner: false, IsActive: true));
        var guard = CreateGuard(grants);

        var result = guard.EnsureCanGrant("owner", "target", new OperationKey("plugins.license", OperationVerb.Import), CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
    }

    private static UserManagementAuthorizationGuard CreateGuard(IPermissionGrantReader grants)
    {
        var registry = OperationTargetRegistry.CreateDefault();
        return new UserManagementAuthorizationGuard(
            new OperationAuthorizationPolicy(registry, grants),
            grants,
            registry);
    }

    private sealed class GuardGrantReader : IPermissionGrantReader
    {
        private readonly Dictionary<string, UserPermissionState> users = new(StringComparer.Ordinal);
        private readonly HashSet<string> grants = new(StringComparer.Ordinal);

        public GuardGrantReader(UserPermissionState actor)
        {
            AddUser(actor);
        }

        public static GuardGrantReader CreateManagedWithGrants(string userId, params OperationKey[] keys)
        {
            var reader = new GuardGrantReader(new UserPermissionState(userId, IsOwner: false, IsActive: true));
            foreach (var key in keys)
            {
                reader.grants.Add($"{userId}:{key}");
            }

            return reader;
        }

        public void AddUser(UserPermissionState state)
        {
            users[state.UserId] = state;
        }

        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return users.TryGetValue(userId, out var state)
                ? Result.Success(state)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(grants.Contains($"{userId}:{key}"));
        }
    }
}
