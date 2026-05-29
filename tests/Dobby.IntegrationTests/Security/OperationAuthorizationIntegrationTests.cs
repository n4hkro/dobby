using Dobby.Configuration;
using Dobby.Audit;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.IntegrationTests.Security;

public sealed class OperationAuthorizationIntegrationTests
{
    [Fact]
    public void Durable_permission_rows_grant_exact_managed_operation_and_owner_needs_no_rows()
    {
        using var fixture = AuthorizationPersistenceFixture.Create();
        var ownerId = fixture.InsertUser("owner", isOwner: true);
        var managedId = fixture.InsertUser("managed", isOwner: false);
        var store = new PermissionGrantStore(fixture.Executor);
        var policy = new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), store);

        policy.Evaluate(new OperationPermissionCapability("users.management.grant"), Context(ownerId))
            .Value.Allowed.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Permissions WHERE UserId = @UserId;", new { UserId = ownerId })
            .ShouldBe(0);

        policy.Evaluate(new OperationPermissionCapability("users.management.view"), Context(managedId))
            .Value.Allowed.ShouldBeFalse();

        store.Grant(managedId, new OperationKey("users.management", OperationVerb.View)).IsSuccess.ShouldBeTrue();

        policy.Evaluate(new OperationPermissionCapability("users.management.view"), Context(managedId))
            .Value.Allowed.ShouldBeTrue();
        policy.Evaluate(new OperationPermissionCapability("users.management.create"), Context(managedId))
            .Value.Allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("missing.target.view")]
    [InlineData("users.management.import")]
    public void Unknown_target_or_unsupported_verb_fails_closed_non_disclosing(string operationKey)
    {
        using var fixture = AuthorizationPersistenceFixture.Create();
        var managedId = fixture.InsertUser("managed", isOwner: false);
        var policy = new OperationAuthorizationPolicy(
            OperationTargetRegistry.CreateDefault(),
            new PermissionGrantStore(fixture.Executor));

        var decision = policy.Evaluate(new OperationPermissionCapability(operationKey), Context(managedId));

        decision.IsSuccess.ShouldBeTrue();
        decision.Value.Allowed.ShouldBeFalse();
        decision.Value.DenyReason.ShouldBe(OperationAuthorizationPolicy.NonDisclosingDenyReason);
    }

    [Fact]
    public void Host_audit_coordinator_records_denied_authorization_attempts_without_security_audit_reference()
    {
        using var fixture = AuthorizationPersistenceFixture.Create();
        var managedId = fixture.InsertUser("managed", isOwner: false);
        var operationKey = "users.management.import";
        var policy = new OperationAuthorizationPolicy(
            OperationTargetRegistry.CreateDefault(),
            new PermissionGrantStore(fixture.Executor));

        var decision = policy.Evaluate(new OperationPermissionCapability(operationKey), Context(managedId));
        if (!decision.Value.Allowed)
        {
            new UserManagementAuditCoordinator(new DurableAuditTrail(fixture.Executor), fixture.Executor, TimeProvider.System)
                .AppendPermissionDenied(managedId, operationKey, CorrelationId.New());
        }

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = @EventName AND ActorId = @ActorId AND Target = @Target AND Result = 'denied';",
            new { EventName = AuditEventName.PermissionDenied, ActorId = managedId, Target = operationKey })
            .ShouldBe(1);
    }

    private static PolicyContext Context(string userId)
    {
        return new PolicyContext(userId, null, CorrelationId.New());
    }

    private sealed class AuthorizationPersistenceFixture : IDisposable
    {
        private AuthorizationPersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            var factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public string InsertUser(string username, bool isOwner)
        {
            var userId = Guid.NewGuid().ToString("D");
            var timestamp = DateTimeOffset.UtcNow.ToString("O");
            Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, @Username, @Username, @IsOwner, 1, @Now, @Now);",
                new { UserId = userId, Username = username, IsOwner = isOwner ? 1 : 0, Now = timestamp });
            return userId;
        }

        public static AuthorizationPersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-authorization-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new AuthorizationPersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
