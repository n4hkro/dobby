using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host.Settings;
using Dobby.Host.Sessions;
using Dobby.Host.Startup;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Auth;

public sealed class UserManagementServiceTests
{
    public static TheoryData<string> CreateFailurePoints => new()
    {
        "user-write",
        "credential-write",
        "audit-write",
        "outbox-write",
        "commit"
    };

    public static TheoryData<string> PasswordResetFailurePoints => new()
    {
        "password-write",
        "session-write",
        "audit-write",
        "outbox-write",
        "commit"
    };

    public static TheoryData<string> DeactivationFailurePoints => new()
    {
        "active-state-write",
        "session-write",
        "audit-write",
        "outbox-write",
        "commit"
    };

    [Fact]
    public void Owner_can_create_list_detail_update_and_results_exclude_credential_material()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();

        var create = service.CreateManagedUser(ownerId, " Worker.One ", "Worker One", "worker password", CorrelationId.New());

        create.IsSuccess.ShouldBeTrue();
        var list = service.ListUsers(ownerId, CorrelationId.New()).Value;
        list.Select(user => user.UsernameNormalized).ShouldContain("worker.one");
        var detail = service.GetUser(ownerId, create.Value, CorrelationId.New()).Value;
        detail.UserId.ShouldBe(create.Value);
        detail.PasswordHash.ShouldBeNull();
        detail.ProtectedCredentialMaterial.ShouldBeNull();

        service.UpdateManagedUser(ownerId, create.Value, "Worker Renamed", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.GetUser(ownerId, create.Value, CorrelationId.New()).Value.DisplayName.ShouldBe("Worker Renamed");
    }

    [Fact]
    public void Managed_user_without_view_grant_cannot_list_or_view_details()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var managedId = service.CreateManagedUser(ownerId, "managed", "Managed", "managed password", CorrelationId.New()).Value;

        service.ListUsers(managedId, CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        service.GetUser(managedId, ownerId, CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void View_granted_managed_user_can_list_but_cannot_create_without_create_grant()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var managedId = service.CreateManagedUser(ownerId, "managed", "Managed", "managed password", CorrelationId.New()).Value;
        service.GrantPermission(ownerId, managedId, "users.management.view", CorrelationId.New()).IsSuccess.ShouldBeTrue();

        service.ListUsers(managedId, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.CreateManagedUser(managedId, "other", "Other", "other password", CorrelationId.New())
            .ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Owner_can_reset_deactivate_and_reactivate_managed_user_without_hard_delete()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var managedId = service.CreateManagedUser(ownerId, "managed", "Managed", "managed password", CorrelationId.New()).Value;
        fixture.SessionLifecycle.Create(managedId).IsSuccess.ShouldBeTrue();

        service.ResetPassword(ownerId, managedId, "new managed password", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE UserId = @UserId AND InvalidatedAtUtc IS NOT NULL AND InvalidationReason = 'password-reset';",
            new { UserId = managedId }).ShouldBe(1);
        fixture.CredentialStore.VerifyPassword(managedId, "new managed password").Value.ShouldBeTrue();

        service.DeactivateUser(ownerId, managedId, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        fixture.LoginService.Login("managed", "new managed password", null, CorrelationId.New())
            .Kind.ShouldBe(LoginAttemptKind.InvalidCredentials);

        service.ReactivateUser(ownerId, managedId, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        fixture.LoginService.Login("managed", "new managed password", null, CorrelationId.New())
            .Kind.ShouldBe(LoginAttemptKind.Success);

        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Users WHERE UserId = @UserId;", new { UserId = managedId })
            .ShouldBe(1);
    }

    [Fact]
    public void Create_managed_user_enforces_service_validation_and_database_uniqueness()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();

        service.CreateManagedUser(ownerId, "ab", "Too Short", "valid password", CorrelationId.New())
            .ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
        service.CreateManagedUser(ownerId, "invalid name", "Invalid", "valid password", CorrelationId.New())
            .ErrorCode.ShouldBe(ErrorCode.ValidationFailed);
        service.CreateManagedUser(ownerId, "valid.user", "Invalid Password", "", CorrelationId.New())
            .ErrorCode.ShouldBe(ErrorCode.ValidationFailed);

        service.CreateManagedUser(ownerId, "duplicate", "Duplicate", "first password", CorrelationId.New())
            .IsSuccess.ShouldBeTrue();
        service.CreateManagedUser(ownerId, " DUPLICATE ", "Duplicate Again", "second password", CorrelationId.New())
            .ErrorCode.ShouldBe(ErrorCode.AlreadyExists);
    }

    [Theory]
    [MemberData(nameof(CreateFailurePoints))]
    public void Managed_user_creation_rolls_back_user_credential_audit_and_outbox_at_every_failure_point(
        string failurePoint)
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var correlationId = CorrelationId.New();

        var result = fixture.CreateService(new UserManagementFailureInjector(failurePoint))
            .CreateManagedUser(
                ownerId,
                "atomic.user",
                "Atomic User",
                "atomic password",
                correlationId);

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE UsernameNormalized = 'atomic.user';").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Credentials WHERE UserId NOT IN (SELECT UserId FROM Users);").ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE CorrelationId = @CorrelationId;",
            new { CorrelationId = correlationId.ToString() }).ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = @IdempotencyKey;",
            new { IdempotencyKey = $"user-mutation:{correlationId}" }).ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_creation_of_same_username_commits_one_complete_user_without_orphans()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var first = fixture.CreateService();
        var second = fixture.CreateService();
        using var start = new Barrier(2);

        var tasks = new[]
        {
            Task.Run(() =>
            {
                start.SignalAndWait();
                return first.CreateManagedUser(
                    ownerId,
                    "same.user",
                    "First",
                    "first password",
                    CorrelationId.New());
            }),
            Task.Run(() =>
            {
                start.SignalAndWait();
                return second.CreateManagedUser(
                    ownerId,
                    "same.user",
                    "Second",
                    "second password",
                    CorrelationId.New());
            })
        };

        var results = await Task.WhenAll(tasks);

        results.Count(result => result.IsSuccess).ShouldBe(1);
        results.Count(result => result.ErrorCode == ErrorCode.AlreadyExists).ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE UsernameNormalized = 'same.user';").ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Credentials WHERE UserId = (SELECT UserId FROM Users WHERE UsernameNormalized = 'same.user');")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.create' AND Target = (SELECT UserId FROM Users WHERE UsernameNormalized = 'same.user');")
            .ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(PasswordResetFailurePoints))]
    public void Password_reset_failure_preserves_old_credential_active_session_and_evidence(
        string failurePoint)
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var normalService = fixture.CreateService();
        var managedId = normalService.CreateManagedUser(
            ownerId,
            "reset.user",
            "Reset User",
            "old password",
            CorrelationId.New()).Value;
        fixture.SessionLifecycle.Create(managedId).IsSuccess.ShouldBeTrue();
        var correlationId = CorrelationId.New();

        var result = fixture.CreateService(new UserManagementFailureInjector(failurePoint))
            .ResetPassword(ownerId, managedId, "new password", correlationId);

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.CredentialStore.VerifyPassword(managedId, "old password").Value.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(managedId, "new password").Value.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;",
            new { UserId = managedId }).ShouldBe(1);
        AssertNoEvidenceForCorrelation(fixture, correlationId);
    }

    [Theory]
    [MemberData(nameof(DeactivationFailurePoints))]
    public void Deactivation_failure_preserves_active_user_session_and_evidence(string failurePoint)
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var normalService = fixture.CreateService();
        var managedId = normalService.CreateManagedUser(
            ownerId,
            "active.user",
            "Active User",
            "active password",
            CorrelationId.New()).Value;
        fixture.SessionLifecycle.Create(managedId).IsSuccess.ShouldBeTrue();
        var correlationId = CorrelationId.New();

        var result = fixture.CreateService(new UserManagementFailureInjector(failurePoint))
            .DeactivateUser(ownerId, managedId, correlationId);

        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        fixture.Executor.QueryScalar<int>(
            "SELECT IsActive FROM Users WHERE UserId = @UserId;",
            new { UserId = managedId }).ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;",
            new { UserId = managedId }).ShouldBe(1);
        AssertNoEvidenceForCorrelation(fixture, correlationId);
    }

    [Fact]
    public void Owner_can_grant_and_revoke_permission_toggle()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var targetId = service.CreateManagedUser(ownerId, "target", "Target", "target password", CorrelationId.New()).Value;

        service.GrantPermission(ownerId, targetId, "users.management.view", CorrelationId.New())
            .IsSuccess.ShouldBeTrue();
        service.GetUser(ownerId, targetId, CorrelationId.New()).Value.PermissionKeys
            .ShouldContain("users.management.view");

        service.RevokePermission(ownerId, targetId, "users.management.view", CorrelationId.New())
            .IsSuccess.ShouldBeTrue();

        service.GetUser(ownerId, targetId, CorrelationId.New()).Value.PermissionKeys
            .ShouldNotContain("users.management.view");
        AuditCount(fixture, AuditEventName.PermissionRevoke, targetId, "success").ShouldBe(1);
    }

    [Fact]
    public void Managed_user_with_matching_grants_can_mutate_only_matching_user_management_operations()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var actorId = service.CreateManagedUser(ownerId, "actor", "Actor", "actor password", CorrelationId.New()).Value;
        var targetId = service.CreateManagedUser(ownerId, "target", "Target", "target password", CorrelationId.New()).Value;
        foreach (var permission in new[]
        {
            "users.management.view",
            "users.management.update",
            "users.management.execute",
            "users.management.delete",
            "users.management.grant"
        })
        {
            service.GrantPermission(ownerId, actorId, permission, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        }

        service.UpdateManagedUser(actorId, targetId, "Target Renamed", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.ResetPassword(actorId, targetId, "reset target password", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        fixture.CredentialStore.VerifyPassword(targetId, "reset target password").Value.ShouldBeTrue();
        service.DeactivateUser(actorId, targetId, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.ReactivateUser(actorId, targetId, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.GrantPermission(actorId, targetId, "users.management.view", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.GetUser(ownerId, targetId, CorrelationId.New()).Value.PermissionKeys
            .ShouldContain("users.management.view");
        service.RevokePermission(actorId, targetId, "users.management.view", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.GetUser(ownerId, targetId, CorrelationId.New()).Value.PermissionKeys
            .ShouldNotContain("users.management.view");
    }

    [Fact]
    public void Deactivation_through_user_management_invalidates_active_session_for_next_checkpoint()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var managedId = service.CreateManagedUser(ownerId, "managed", "Managed", "managed password", CorrelationId.New()).Value;
        var session = fixture.SessionLifecycle.Create(managedId).Value;

        service.DeactivateUser(ownerId, managedId, CorrelationId.New()).IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<string>(
            "SELECT InvalidationReason FROM Sessions WHERE SessionId = @SessionId;",
            new { session.SessionId }).ShouldBe("deactivated");
        var checkpoint = fixture.SessionLifecycle.Checkpoint();
        checkpoint.Kind.ShouldBe(SessionCheckpointKind.Invalidated);
        checkpoint.Reason.ShouldBe("deactivated");
        new SessionLifecycleAuditCoordinator(new DurableAuditTrail(fixture.Executor), TimeProvider.System)
            .AppendCheckpoint(managedId, checkpoint, CorrelationId.New());
        AuditCount(fixture, AuditEventName.SessionInvalidated, "host-session", "success").ShouldBe(1);
    }

    [Fact]
    public void Managed_user_cannot_touch_owner_self_grant_or_grant_absent_permission()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var actorId = service.CreateManagedUser(ownerId, "actor", "Actor", "actor password", CorrelationId.New()).Value;
        var targetId = service.CreateManagedUser(ownerId, "target", "Target", "target password", CorrelationId.New()).Value;
        service.GrantPermission(ownerId, actorId, "users.management.grant", CorrelationId.New()).IsSuccess.ShouldBeTrue();

        service.DeactivateUser(actorId, ownerId, CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        service.GrantPermission(actorId, actorId, "users.management.view", CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        service.GrantPermission(actorId, targetId, "plugins.license.import", CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
    }

    [Fact]
    public void Owner_cannot_grant_unknown_or_unsupported_permission_keys()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var targetId = service.CreateManagedUser(ownerId, "target", "Target", "target password", CorrelationId.New()).Value;

        service.GrantPermission(ownerId, targetId, "missing.target.view", CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        service.GrantPermission(ownerId, targetId, "users.management.import", CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Permissions WHERE UserId = @UserId;",
            new { UserId = targetId }).ShouldBe(0);
    }

    [Fact]
    public void User_management_operations_append_durable_audit_events()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();

        var targetId = service.CreateManagedUser(ownerId, "audited", "Audited", "audited password", CorrelationId.New()).Value;
        service.GrantPermission(ownerId, targetId, "users.management.view", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.ResetPassword(ownerId, targetId, "new audited password", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.DeactivateUser(ownerId, targetId, CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.ReactivateUser(ownerId, targetId, CorrelationId.New()).IsSuccess.ShouldBeTrue();

        AuditCount(fixture, AuditEventName.UserCreate, targetId, "success").ShouldBe(1);
        AuditCount(fixture, AuditEventName.PermissionGrant, targetId, "success").ShouldBe(1);
        AuditCount(fixture, AuditEventName.UserPasswordReset, targetId, "success").ShouldBe(1);
        AuditCount(fixture, AuditEventName.UserDeactivate, targetId, "success").ShouldBe(1);
        AuditCount(fixture, AuditEventName.UserActivate, targetId, "success").ShouldBe(1);
    }

    [Fact]
    public void User_management_denials_append_durable_audit_events()
    {
        using var fixture = UserManagementFixture.Create();
        var ownerId = fixture.BootstrapOwner();
        var service = fixture.CreateService();
        var actorId = service.CreateManagedUser(ownerId, "actor", "Actor", "actor password", CorrelationId.New()).Value;
        var targetId = service.CreateManagedUser(ownerId, "target", "Target", "target password", CorrelationId.New()).Value;
        service.GrantPermission(ownerId, actorId, "users.management.grant", CorrelationId.New()).IsSuccess.ShouldBeTrue();
        service.GrantPermission(ownerId, actorId, "users.management.delete", CorrelationId.New()).IsSuccess.ShouldBeTrue();

        service.CreateManagedUser(actorId, "denied", "Denied", "denied password", CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        service.DeactivateUser(actorId, ownerId, CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);
        service.GrantPermission(actorId, targetId, "missing.target.view", CorrelationId.New()).ErrorCode.ShouldBe(ErrorCode.Forbidden);

        AuditCount(fixture, AuditEventName.PermissionDenied, "users.management.create", "denied").ShouldBe(1);
        AuditCount(fixture, AuditEventName.OwnerProtectionDenied, ownerId, "denied").ShouldBe(1);
        AuditCount(fixture, AuditEventName.PermissionDenied, "missing.target.view", "denied").ShouldBe(1);
    }

    private sealed class UserManagementFixture : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly KdfPolicyResolver resolver = new(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });
        private readonly FakeTimeProvider time = new(DateTimeOffset.Parse("2026-05-25T00:00:00Z"));

        private UserManagementFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            CredentialStore = new CredentialStore(Executor, protector, resolver);
            SessionLifecycle = new SessionLifecycleService(
                new SessionStore(Executor),
                new ActiveSessionAccessor(),
                CredentialStore,
                time,
                new SessionOptions());
            LoginService = new LoginService(
                Executor,
                resolver,
                CredentialStore,
                new OwnerBootstrapCoordinator(new HostPersistenceTransactionScope(Factory), new OwnerBootstrapService(protector, resolver), new DurableAuditTrail(Executor), time),
                new LoginThrottleService([0, 0, 10], time));
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public CredentialStore CredentialStore { get; }

        public SessionLifecycleService SessionLifecycle { get; }

        public LoginService LoginService { get; }

        public string BootstrapOwner()
        {
            return LoginService.Login("owner", "owner password", "Owner", CorrelationId.New()).UserId!;
        }

        public ManagedUserService CreateService(IHostPersistenceTransactionFaultInjector? injector = null)
        {
            var grants = new PermissionGrantStore(Executor);
            var registry = OperationTargetRegistry.CreateDefault();
            var policy = new OperationAuthorizationPolicy(registry, grants);
            var mutationUnitOfWork = new HostMutationUnitOfWork(
                new HostPersistenceTransactionScope(Factory, injector),
                new DurableAuditTrail(Executor),
                new HostTransactionOutbox(time));
            return new ManagedUserService(
                Executor,
                CredentialStore,
                grants,
                new UserManagementAuthorizationGuard(policy, grants, registry),
                time,
                new UserManagementAuditCoordinator(
                    new DurableAuditTrail(Executor),
                    Executor,
                    time,
                    mutationUnitOfWork));
        }

        public static UserManagementFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-user-management-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new UserManagementFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class UserManagementFailureInjector(string failurePoint)
        : IHostPersistenceTransactionFaultInjector
    {
        public void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText)
        {
            if ((failurePoint == "user-write" && commandText.Contains("INSERT INTO Users", StringComparison.Ordinal)) ||
                (failurePoint == "credential-write" && commandText.Contains("INSERT INTO Credentials", StringComparison.Ordinal)) ||
                (failurePoint == "password-write" && commandText.Contains("UPDATE Credentials", StringComparison.Ordinal)) ||
                (failurePoint == "active-state-write" && commandText.Contains("UPDATE Users SET IsActive", StringComparison.Ordinal)) ||
                (failurePoint == "session-write" && commandText.Contains("UPDATE Sessions", StringComparison.Ordinal)) ||
                (failurePoint == "audit-write" && commandText.Contains("INSERT INTO AuditLog", StringComparison.Ordinal)) ||
                (failurePoint == "outbox-write" && commandText.Contains("INSERT INTO HostOutbox", StringComparison.Ordinal)))
            {
                throw new SyntheticUserManagementFailure(failurePoint);
            }
        }

        public void BeforeCommit()
        {
            if (failurePoint == "commit")
            {
                throw new SyntheticUserManagementFailure(failurePoint);
            }
        }
    }

    private sealed class SyntheticUserManagementFailure(string failurePoint)
        : Exception("Synthetic User Management failure at " + failurePoint);

    private static int AuditCount(UserManagementFixture fixture, string eventName, string target, string result)
    {
        return fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE EventName = @EventName AND Target = @Target AND Result = @Result;",
            new { EventName = eventName, Target = target, Result = result });
    }

    private static void AssertNoEvidenceForCorrelation(
        UserManagementFixture fixture,
        CorrelationId correlationId)
    {
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE CorrelationId = @CorrelationId;",
            new { CorrelationId = correlationId.ToString() }).ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = @IdempotencyKey;",
            new { IdempotencyKey = $"user-mutation:{correlationId}" }).ShouldBe(0);
    }

    private sealed class RecordingProtector : IProtectedDataProtector
    {
        private static readonly byte[] ProtectedPrefix = "protected:"u8.ToArray();

        public byte[] Protect(byte[] plaintext, byte[] entropy)
        {
            return ProtectedPrefix.Concat(plaintext.Select(value => (byte)(value ^ 0x5A))).ToArray();
        }

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy)
        {
            protectedBlob.Take(ProtectedPrefix.Length).SequenceEqual(ProtectedPrefix).ShouldBeTrue();
            return protectedBlob.Skip(ProtectedPrefix.Length).Select(value => (byte)(value ^ 0x5A)).ToArray();
        }
    }
}
