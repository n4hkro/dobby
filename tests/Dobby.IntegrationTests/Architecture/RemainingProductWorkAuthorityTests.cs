using Dobby.Audit;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host;
using Dobby.Host.Plugins;
using Dobby.Host.Startup;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.IntegrationTests.Architecture;

public sealed class RemainingProductWorkAuthorityTests
{
    [Fact]
    public void Canonical_runtime_storage_auth_and_audit_authority_is_preserved()
    {
        RuntimeFolderLayout.RequiredFolderNames.ShouldBe(
        [
            "data",
            "config",
            "logs",
            "plugins",
            "licenses",
            "recovery",
            "backup",
            "temp"
        ]);

        using var diagnosticsRoot = TemporaryDirectory.Create("dobby-authority-diagnostics-");
        var logsRoot = Path.Combine(diagnosticsRoot.Path, "logs");
        Directory.CreateDirectory(logsRoot);
        File.WriteAllText(Path.Combine(logsRoot, "host.log"), "startup ok", Encoding.UTF8);
        var exportPath = Path.Combine(diagnosticsRoot.Path, "temp", "support", "dobby-diagnostics.zip");
        File.Exists(exportPath).ShouldBeFalse();

        var diagnosticsPackage = new DiagnosticsExportPackageBuilder().Build(logsRoot, exportPath);

        diagnosticsPackage.Path.ShouldBe(exportPath);
        diagnosticsPackage.IncludedEntries.ShouldBe(["host.log"]);
        File.Exists(exportPath).ShouldBeTrue();
        Directory.Exists(Path.Combine(diagnosticsRoot.Path, "diagnostics")).ShouldBeFalse();

        using var persistence = AuthorityPersistenceFixture.Create();
        persistence.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();

        var stagingTableCount = persistence.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'PluginInstallStaging';");
        stagingTableCount.ShouldBe(1);

        var staging = new PluginInstallStagingArea(persistence.Root, persistence.Executor);
        var package = CreatePluginPackage();
        var stagedPath = staging.Stage(package).Value;
        stagedPath.ShouldStartWith(Path.Combine(persistence.Root, "plugins", "staging"), Case.Insensitive);
        persistence.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallStaging;").ShouldBe(1);

        var promotedPath = staging.Promote(stagedPath, package).Value;
        promotedPath.ShouldStartWith(Path.Combine(persistence.Root, "plugins", "installed"), Case.Insensitive);
        persistence.Executor.QueryScalar<string>("SELECT RuntimePath FROM PluginInstallStaging;").ShouldBe(promotedPath);

        var auditColumns = persistence.Executor.Query<string>("SELECT name FROM pragma_table_info('AuditLog');")
            .ToHashSet(StringComparer.Ordinal);
        foreach (var expectedColumn in new[]
                 {
                     "OccurredAtUtc",
                     "EventName",
                     "ActorId",
                     "Target",
                     "Result",
                     "CorrelationId",
                     "DetailsJson"
                 })
        {
            auditColumns.ShouldContain(expectedColumn);
        }
        auditColumns.ShouldNotContain("Severity");
        typeof(AuditOptions).GetProperty("Severity").ShouldBeNull();

        var correlationId = CorrelationId.New();
        var auditPayload = new AuditEvent(
            AuditEventName.UserLogin,
            "owner",
            "host",
            AuditResult.Success,
            correlationId,
            DateTimeOffset.Parse("2026-07-08T00:00:00Z"),
            new Dictionary<string, string> { ["culture"] = "az-AZ" })
            .ToCanonicalPayload();
        auditPayload.Keys.ShouldBe(
        [
            "timestamp",
            "actor",
            "action",
            "target",
            "result",
            "correlationId",
            "details"
        ]);
        auditPayload["correlationId"].ShouldBe(correlationId.ToString());
        auditPayload.ShouldNotContainKey("severity");

        var auditPayloadWithoutDetails = new AuditEvent(
            AuditEventName.UserLogin,
            "owner",
            "host",
            AuditResult.Success,
            CorrelationId.New(),
            DateTimeOffset.Parse("2026-07-08T00:00:01Z"))
            .ToCanonicalPayload();
        auditPayloadWithoutDetails.Keys.ShouldBe(
        [
            "timestamp",
            "actor",
            "action",
            "target",
            "result",
            "correlationId"
        ]);
        auditPayloadWithoutDetails.ShouldNotContainKey("details");
        auditPayloadWithoutDetails.ShouldNotContainKey("severity");

        var localizationEvent = new LocalizationMissingKeyEvent(
            "az-AZ",
            "Plugins.InstallButton",
            "plugin:sample",
            "found-in-fallback");
        localizationEvent.EventName.ShouldBe("localization.missing-key");
        localizationEvent.Severity.ShouldBe("Warning");

        DisplayNameValidator.Normalize(null).ShouldBeNull();
        DisplayNameValidator.Normalize("   ").ShouldBeNull();
        DisplayNameValidator.Normalize("  Əli Məmmədov  ").ShouldBe("Əli Məmmədov");
        DisplayNameValidator.Normalize("  OWNER Şəxs  ").ShouldBe("OWNER Şəxs");
        persistence.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES ('user-1', 'user.one', 'Əli Məmmədov', 0, 1, @Now, @Now);",
            new { Now = DateTimeOffset.UtcNow.ToString("O") });
        persistence.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES ('user-2', 'user.two', 'Əli Məmmədov', 0, 1, @Now, @Now);",
            new { Now = DateTimeOffset.UtcNow.ToString("O") });
        persistence.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Users WHERE DisplayName = 'Əli Məmmədov';").ShouldBe(2);
        using var displayNameFixture = LoginAuthorityFixture.Create("Pbkdf2Sha256");
        var bootstrapResult = displayNameFixture.CreateService().Login(
            "owner",
            "first owner password",
            " owner.alias ",
            CorrelationId.New());
        bootstrapResult.Kind.ShouldBe(LoginAttemptKind.Success);
        bootstrapResult.UserId.ShouldNotBeNullOrWhiteSpace();
        displayNameFixture.Executor.QueryScalar<string>(
            "SELECT UsernameNormalized FROM Users WHERE UserId = @UserId;",
            new { UserId = bootstrapResult.UserId }).ShouldBe("owner");
        displayNameFixture.Executor.QueryScalar<string?>(
            "SELECT DisplayName FROM Users WHERE UserId = @UserId;",
            new { UserId = bootstrapResult.UserId }).ShouldBe("owner.alias");
        displayNameFixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE UsernameNormalized = 'owner.alias';").ShouldBe(0);
        displayNameFixture.CreateService()
            .Login("owner.alias", "first owner password", null, CorrelationId.New())
            .Kind.ShouldBe(LoginAttemptKind.InvalidCredentials);

        PasswordPolicyValidator.Validate(" ").IsSuccess.ShouldBeFalse();
        PasswordPolicyValidator.Validate("a").IsSuccess.ShouldBeTrue();
        PasswordPolicyValidator.Validate("correct horse battery staple").IsSuccess.ShouldBeTrue();
        PasswordPolicyValidator.Validate(new string('a', 128)).IsSuccess.ShouldBeTrue();
        PasswordPolicyValidator.Validate(new string('a', 129)).IsSuccess.ShouldBeFalse();

        var validator = new DobbyOptionsValidator();
        validator.Validate(Options.DefaultName, new DobbyOptions
        {
            Auth = new AuthOptions { PasswordKdfMode = "Argon2id" }
        }).Succeeded.ShouldBeTrue();

        var pbkdf2Resolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" });
        pbkdf2Resolver.Resolve().Value.ShouldBe(new KdfPolicy("PBKDF2-HMAC-SHA256", 600_000, 16, 32));

        var unsupportedResolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Argon2id" });
        var unsupportedPolicy = unsupportedResolver.Resolve();
        unsupportedPolicy.IsSuccess.ShouldBeFalse();
        unsupportedPolicy.ErrorCode.ShouldBe(ErrorCode.Unauthorized);

        using var loginFixture = LoginAuthorityFixture.Create("Argon2id");
        var bootstrapFailure = loginFixture.CreateBootstrapCoordinator()
            .BootstrapFirstOwner("owner", "owner.alias", "first owner password", CorrelationId.New());
        bootstrapFailure.IsSuccess.ShouldBeFalse();
        bootstrapFailure.ErrorCode.ShouldBe(ErrorCode.Unauthorized);

        var loginResult = loginFixture.CreateService().Login("owner", "first owner password", "owner.alias", CorrelationId.New());
        loginResult.Kind.ShouldBe(LoginAttemptKind.KdfUnsupported);
        loginFixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;").ShouldBe(0);
        loginFixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Credentials;").ShouldBe(0);
        loginFixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog;").ShouldBe(0);
    }

    private static PluginPackageDescriptor CreatePluginPackage()
    {
        var fileContent = Encoding.UTF8.GetBytes("authority guardrail plugin");
        var fileHash = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();
        var manifest = new PluginManifest(
            "com.dobby.authority",
            "dobby.vendor",
            "1.0.0",
            "1.0.0",
            "bin/plugins/com.dobby.authority.dll",
            "Authority Guardrail",
            "Authority guardrail package",
            null,
            [],
            [new PluginCapability("plugin-page", "plugin-page")],
            [],
            [],
            [new PluginPackageFile("content/readme.txt", fileHash)],
            new PluginPackageSignature("ECDSA-P256-SHA256", 1, "AQ"));
        var fingerprint = PluginPackageFingerprint.Compute(manifest);
        var manifestJson =
            "{\"pluginId\":\"com.dobby.authority\",\"version\":\"1.0.0\",\"vendorId\":\"dobby.vendor\",\"minHostVersion\":\"1.0.0\",\"entryAssembly\":\"bin/plugins/com.dobby.authority.dll\",\"displayName\":\"Authority Guardrail\",\"description\":\"Authority guardrail package\",\"dependsOn\":[],\"capabilities\":[{\"name\":\"plugin-page\",\"scope\":\"plugin-page\"}],\"operationTargets\":[],\"extensionPoints\":[],\"files\":[{\"path\":\"content/readme.txt\",\"sha256\":\"" +
            fileHash +
            "\"}],\"signature\":{\"algorithm\":\"ECDSA-P256-SHA256\",\"keyVersion\":1,\"value\":\"AQ\"}}";
        return new PluginPackageDescriptor(
            "authority.dobbyplugin",
            fingerprint.Fingerprint,
            fingerprint.CanonicalPayload,
            manifest,
            [
                new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))).ToLowerInvariant(), false),
                new PluginPackageEntry("content/readme.txt", fileContent, fileHash, false)
            ],
            PluginPackageReader.ManifestFileName,
            manifestJson);
    }

    private sealed class AuthorityPersistenceFixture : IDisposable
    {
        private AuthorityPersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            Migrator = new HostPersistenceMigrator(Factory, probe);
            Executor = new HostPersistenceCommandExecutor(Factory);
            Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public static AuthorityPersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-authority-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new AuthorityPersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class LoginAuthorityFixture : IDisposable
    {
        private readonly RecordingProtector protector = new();
        private readonly string passwordKdfMode;

        private LoginAuthorityFixture(string root, string passwordKdfMode)
        {
            Root = root;
            this.passwordKdfMode = passwordKdfMode;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, probe);
            new HostPersistenceMigrator(Factory, probe).ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(Factory);
            Time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-08T00:00:00Z"));
            Throttle = new LoginThrottleService([0, 0, 10, 30, 60], Time);
        }

        public string Root { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public FakeTimeProvider Time { get; }

        public LoginThrottleService Throttle { get; }

        public LoginService CreateService()
        {
            var resolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = passwordKdfMode });
            var credentialStore = new CredentialStore(Executor, protector, resolver);
            return new LoginService(Executor, resolver, credentialStore, CreateBootstrapCoordinator(), Throttle);
        }

        public OwnerBootstrapCoordinator CreateBootstrapCoordinator()
        {
            var resolver = new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = passwordKdfMode });
            return new OwnerBootstrapCoordinator(
                new HostPersistenceTransactionScope(Factory),
                new OwnerBootstrapService(protector, resolver),
                new DurableAuditTrail(Executor),
                Time);
        }

        public static LoginAuthorityFixture Create(string passwordKdfMode)
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-authority-login-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new LoginAuthorityFixture(root, passwordKdfMode);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
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

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create(string prefix)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
