using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class OperationAuthorizationPolicyTests
{
    [Fact]
    public void Owner_is_allowed_for_registered_operations_without_permission_rows()
    {
        var grants = new RecordingGrantReader(new UserPermissionState("owner", IsOwner: true, IsActive: true));
        var policy = new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants);

        var decision = policy.Evaluate(
            new OperationPermissionCapability("users.management.grant"),
            Context("owner"));

        decision.IsSuccess.ShouldBeTrue();
        decision.Value.Allowed.ShouldBeTrue();
        grants.HasGrantCalls.ShouldBe(0);
    }

    [Fact]
    public void Managed_user_without_grant_is_denied_with_non_disclosing_reason()
    {
        var policy = new OperationAuthorizationPolicy(
            OperationTargetRegistry.CreateDefault(),
            new RecordingGrantReader(new UserPermissionState("managed", IsOwner: false, IsActive: true)));

        var decision = policy.Evaluate(
            new OperationPermissionCapability("users.management.create"),
            Context("managed"));

        decision.IsSuccess.ShouldBeTrue();
        decision.Value.Allowed.ShouldBeFalse();
        decision.Value.DenyReason.ShouldBe(OperationAuthorizationPolicy.NonDisclosingDenyReason);
    }

    [Fact]
    public void Exact_grant_allows_managed_user_only_for_that_operation()
    {
        var grants = new RecordingGrantReader(new UserPermissionState("managed", IsOwner: false, IsActive: true));
        grants.Grant("managed", new OperationKey("users.management", OperationVerb.View));
        var policy = new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants);

        policy.Evaluate(new OperationPermissionCapability("users.management.view"), Context("managed"))
            .Value.Allowed.ShouldBeTrue();
        policy.Evaluate(new OperationPermissionCapability("users.management.update"), Context("managed"))
            .Value.Allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("missing.target.view")]
    [InlineData("users.management.import")]
    [InlineData("users.management.unknown")]
    public void Unknown_target_or_verb_denies_without_disclosing_details(string operationKey)
    {
        var policy = new OperationAuthorizationPolicy(
            OperationTargetRegistry.CreateDefault(),
            new RecordingGrantReader(new UserPermissionState("managed", IsOwner: false, IsActive: true)));

        var decision = policy.Evaluate(new OperationPermissionCapability(operationKey), Context("managed"));

        decision.IsSuccess.ShouldBeTrue();
        decision.Value.Allowed.ShouldBeFalse();
        decision.Value.DenyReason.ShouldBe(OperationAuthorizationPolicy.NonDisclosingDenyReason);
    }

    private static PolicyContext Context(string userId)
    {
        return new PolicyContext(userId, null, CorrelationId.New());
    }

    private sealed class RecordingGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        private readonly HashSet<string> grants = new(StringComparer.Ordinal);

        public int HasGrantCalls { get; private set; }

        public void Grant(string userId, OperationKey key)
        {
            grants.Add($"{userId}:{key}");
        }

        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return StringComparer.Ordinal.Equals(userId, state.UserId)
                ? Result.Success(state)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            HasGrantCalls++;
            return Result.Success(grants.Contains($"{userId}:{key}"));
        }
    }
}
