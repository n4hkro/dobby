using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dobby.Audit;
using Dobby.Admin.Commands;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Localization;
using Dobby.Messaging;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using Dobby.Shell;
using Shouldly;

namespace Dobby.SmokeTests;

public sealed class ShellSmokeTests
{
    private const string DevQaSigningKeyPathVariable = "DOBBY_ADMIN_SIGNING_DEV_QA_PLUGIN_PACKAGE_KEY_PATH";
    private const string DevQaRecoveryKeyPathVariable = "DOBBY_ADMIN_SIGNING_DEV_QA_OWNER_RECOVERY_RESPONSE_KEY_PATH";

    [Fact]
    public void Shell_view_model_exposes_home_plugins_settings_in_stable_order()
    {
        var shell = new ShellViewModel(new HostStringCatalog());

        shell.TopNavigation.Select(x => x.Label).ShouldBe(["Ana səhifə", "Pluginlər", "Tənzimləmələr"]);
    }

    [Fact]
    public void Admin_command_host_rejects_unapproved_option_surfaces()
    {
        var result = AdminCommandHost.Run(["--cli", "create-recovery-response", "--unapproved-option"]);

        result.ExitCode.ShouldBe(64);
        result.StandardError.ShouldContain("option contract is not approved");
    }

    [Fact]
    public void Admin_cli_process_rejects_unapproved_option_without_stack_trace()
    {
        var adminExe = FindBuiltAdminExe();
        var result = RunAdminProcess(adminExe, ["--cli", "create-recovery-response", "--unapproved-option"]);

        result.ExitCode.ShouldBe(64, result.Output + result.Error);
        result.Error.ShouldContain("option contract is not approved");
        result.Error.ShouldNotContain(" at ");
    }

    [Fact]
    public void Admin_cli_process_returns_validation_conflict_without_secret_paths_or_stack_trace()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        var outputPath = fixture.Path("signed.dobbyplugin");
        File.WriteAllText(outputPath, "already exists", new UTF8Encoding(false));

        var result = RunAdminProcess(adminExe, [
            "--cli",
            "sign-plugin-package",
            "--input-path",
            fixture.Root,
            "--signing-profile",
            "dev-qa",
            "--output-path",
            outputPath
        ]);

        result.ExitCode.ShouldBe(65, result.Output + result.Error);
        result.Error.ShouldContain("output-conflict");
        result.Error.ShouldNotContain(fixture.Root);
        result.Error.ShouldNotContain(" at ");
    }

    [Fact]
    public void Admin_cli_process_returns_unavailable_signing_material_without_secret_paths_or_stack_trace()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        File.WriteAllText(requestPath, "{}", new UTF8Encoding(false));

        var result = RunAdminProcess(
            adminExe,
            [
                "--cli",
                "create-recovery-response",
                "--request-path",
                requestPath,
                "--device-public-key-path",
                fixture.Path("device-public-key.der"),
                "--signing-profile",
                "dev-qa",
                "--output-path",
                outputPath
            ],
            startInfo => startInfo.Environment.Remove(DevQaRecoveryKeyPathVariable));

        result.ExitCode.ShouldBe(66, result.Output + result.Error);
        result.Error.ShouldContain("signing-material-unavailable");
        result.Error.ShouldNotContain(fixture.Root);
        result.Error.ShouldNotContain(" at ");
    }

    [Fact]
    public void Admin_cli_process_creates_recovery_response_with_environment_signing_material()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyPath = fixture.Path("dev-qa-private-key.pem");
        var outputPath = fixture.Path("response.dobbyrecovery");
        File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
        var request = WriteRecoveryRequest(fixture);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(adminExe)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        process.StartInfo.Environment[DevQaRecoveryKeyPathVariable] = keyPath;
        process.StartInfo.ArgumentList.Add("--cli");
        process.StartInfo.ArgumentList.Add("create-recovery-response");
        process.StartInfo.ArgumentList.Add("--request-path");
        process.StartInfo.ArgumentList.Add(request.RequestPath);
        process.StartInfo.ArgumentList.Add("--device-public-key-path");
        process.StartInfo.ArgumentList.Add(request.PublicKeyPath);
        process.StartInfo.ArgumentList.Add("--signing-profile");
        process.StartInfo.ArgumentList.Add("dev-qa");
        process.StartInfo.ArgumentList.Add("--output-path");
        process.StartInfo.ArgumentList.Add(outputPath);

        process.Start().ShouldBeTrue();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Admin CLI smoke timed out.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.ExitCode.ShouldBe(0, output + error);
        File.Exists(outputPath).ShouldBeTrue();
        output.ShouldContain("create-recovery-response");
        output.ShouldNotContain(fixture.Root);
        error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sample_plugin_smoke_signs_installs_launches_and_exercises_main_actions()
    {
        var adminExe = FindBuiltAdminExe();
        using var fixture = TempFixture.Create();
        using var persistence = PluginSmokePersistenceFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signedPackagePath = fixture.Path("sample.signed.dobbyplugin");
        var keyPath = fixture.Path("dev-qa-private-key.pem");
        File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
        var sign = RunAdminProcess(
            adminExe,
            [
                "--cli",
                "sign-plugin-package",
                "--input-path",
                FindSamplePackageCandidate(),
                "--signing-profile",
                "dev-qa",
                "--output-path",
                signedPackagePath
            ],
            startInfo => startInfo.Environment[DevQaSigningKeyPathVariable] = keyPath);
        sign.ExitCode.ShouldBe(0, sign.Output + sign.Error);
        File.Exists(signedPackagePath).ShouldBeTrue();

        persistence.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();
        var stateStore = new HostInstalledPluginStateStore(persistence.Executor, persistence.TransactionScope);
        var metadataCache = new HostInstalledPluginMetadataCache(persistence.Executor, persistence.TransactionScope);
        var staging = new PluginInstallStagingArea(persistence.Root, persistence.Executor);
        var operationTargets = OperationTargetRegistry.CreateDefault();
        var grants = new MutableGrantReader(new UserPermissionState("managed", IsOwner: false, IsActive: true));
        grants.Grant("managed", new OperationKey(BuiltInOperationTargets.PluginManagementTargetId, OperationVerb.Import));
        var authorization = new OperationAuthorizationPolicy(operationTargets, grants);
        var audit = new PluginRuntimeAuditCoordinator(new InMemoryAuditTrail(), TimeProvider.System);
        var trustVerifier = new DevQaSigningKeyTrustVerifier(key);
        var installer = new HostPluginPackageInstallCoordinator(
            stateStore,
            metadataCache,
            staging,
            authorization,
            audit,
            trustVerifier,
            new HostRuntimeModeMutationGate(_ => Result.Success(PluginRuntimeMode.FullAccess)));

        var install = installer.InstallPackage(
            "managed",
            signedPackagePath,
            new HostVersion(1, 0, 0),
            CorrelationId.New());

        install.IsSuccess.ShouldBeTrue(install.IsSuccess ? null : install.ErrorCode.ToString());
        await using var lifetimes = new PluginLifetimeManager();
        var extensionRegistry = new PluginExtensionPointRegistry();
        var reporter = new RecordingBackgroundOperationReporter();
        var activationCancellation = new HostPluginActivationCancellation();
        var workSessions = new RecordingPluginWorkSessionService();
        var pluginHostServices = new PluginHostServices(
            new InMemoryEventBus(),
            reporter,
            activationCancellation,
            operationAuthorizationPolicy: authorization,
            pluginWorkSessions: workSessions);
        var runtime = new HostPluginRuntimeCoordinator(
            persistence.Migrator,
            stateStore,
            metadataCache,
            new HostPluginActivationStatusPersistence(stateStore, metadataCache, persistence.TransactionScope),
            staging,
            new PluginDataRetentionStore(persistence.Root, persistence.TransactionScope),
            operationTargets,
            extensionRegistry,
            lifetimes,
            activationCancellation,
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionRegistry, lifetimes, pluginHostServices)),
            new PluginCurrentProcessActivationTracker(),
            trustVerifier,
            audit,
            reporter);

        var activation = await runtime.BeginPostLoginActivation(new HostVersion(1, 0, 0), "managed");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        activation.Value.Run.Outcomes.Single().Status.ShouldBe(
            PluginActivationStatus.Active,
            activation.Value.Run.Outcomes.Single().Reason);
        activation.Value.Run.NavigationContributions.Select(item => item.RouteKey)
            .ShouldBe(["plugin/com.dobby.sample/sample", "plugin/com.dobby.sample/greet"]);
        var shell = ShellViewModel.CreateDefault(new HostStringCatalog());
        shell.ApplyPluginNavigationContributions(
            PluginNavigationContributionMapper.Map(activation.Value.Run.NavigationContributions));
        var pageCoordinator = new PluginPageRouteCoordinator(extensionRegistry, shell, () => "managed");
        var route = await pageCoordinator.ActivateRouteAsync("plugin/com.dobby.sample/greet", CancellationToken.None);
        route.IsSuccess.ShouldBeTrue();
        var pageModel = shell.ActivePluginPageModel.ShouldBeOfType<PluginActionPageModel>();

        var greet = pageModel.Actions.Single(action => action.ActionId == "sample.greet");
        var denied = await greet.ExecuteAsync(pageModel.Context, CancellationToken.None);
        denied.IsSuccess.ShouldBeFalse();
        denied.ErrorCode.ShouldBe(ErrorCode.Forbidden);

        grants.Grant("managed", new OperationKey("com.dobby.sample.greet", OperationVerb.Execute));
        var success = await greet.ExecuteAsync(pageModel.Context, CancellationToken.None);
        success.IsSuccess.ShouldBeTrue();
        success.Value.MessageKey.ShouldBe("sample.greet.success");

        var invalid = await pageModel.Actions.Single(action => action.ActionId == "sample.invalid-input")
            .ExecuteAsync(pageModel.Context, CancellationToken.None);
        invalid.IsSuccess.ShouldBeFalse();
        invalid.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        var faultAction = pageModel.Actions.Single(action => action.ActionId == "sample.fault");
        var fault = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await faultAction.ExecuteAsync(pageModel.Context, CancellationToken.None));
        fault.Message.ShouldBe("sample.fault.unhandled");
        var progress = await pageModel.Actions.Single(action => action.ActionId == "sample.progress")
            .ExecuteAsync(pageModel.Context, CancellationToken.None);
        progress.IsSuccess.ShouldBeTrue();
        reporter.Snapshots.ShouldContain(snapshot =>
            snapshot.OwnerKind == BackgroundOperationOwnerKind.Plugin &&
            snapshot.OwnerId == "com.dobby.sample" &&
            snapshot.Status == BackgroundOperationStatus.Completed);
        var workSession = await pageModel.Actions.Single(action => action.ActionId == "sample.work-session")
            .ExecuteAsync(pageModel.Context, CancellationToken.None);
        workSession.IsSuccess.ShouldBeTrue();
        workSessions.Events.ShouldContain("open:com.dobby.sample:plugin/com.dobby.sample/greet:sample.work-session");
        workSessions.Events.ShouldContain("progress:50");
        workSessions.Events.ShouldContain("result:sample.work-session.completed");
    }

    private static string FindBuiltAdminExe()
    {
        var root = RepoRoot().FullName;
        var candidates = new[]
        {
            Path.Combine(root, "src", "Dobby.Admin", "bin", "Release", "net10.0-windows", "Dobby.Admin.exe"),
            Path.Combine(root, "src", "Dobby.Admin", "bin", "Debug", "net10.0-windows", "Dobby.Admin.exe")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Smoke requires a built Dobby.Admin.exe artifact.");
    }

    private static string FindSamplePackageCandidate()
    {
        var root = RepoRoot().FullName;
        var candidates = new[]
        {
            System.IO.Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Release", "net10.0", "package"),
            System.IO.Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Debug", "net10.0", "package")
        };

        return candidates.FirstOrDefault(Directory.Exists)
            ?? throw new DirectoryNotFoundException("Smoke requires the built sample plugin package candidate.");
    }

    private static AdminProcessResult RunAdminProcess(
        string adminExe,
        IReadOnlyList<string> arguments,
        Action<ProcessStartInfo>? configure = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(adminExe)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        configure?.Invoke(process.StartInfo);
        process.Start().ShouldBeTrue();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Admin CLI smoke timed out.");
        }

        return new AdminProcessResult(
            process.ExitCode,
            process.StandardOutput.ReadToEnd(),
            process.StandardError.ReadToEnd());
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }

    private static RecoveryRequestFiles WriteRecoveryRequest(TempFixture fixture)
    {
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = deviceKey.ExportSubjectPublicKeyInfo();
        var created = new RecoveryRequestExporter(
            new DeviceRecoveryKeySigner(deviceKey.ExportPkcs8PrivateKey(), publicKey),
            new RecoveryAuthorityContext("customer-1", "deployment-1", "device:smoke-1"),
            TimeProvider.System,
            VendorRecoveryPublicKey.DevQaKeyId).CreateRequest();
        created.IsSuccess.ShouldBeTrue(created.ErrorCode?.ToString());
        var requestPath = fixture.Path("request-v2.dobbyrecoveryrequest");
        var publicKeyPath = fixture.Path("device-public-key.der");
        File.WriteAllText(requestPath, RecoveryRequestExporter.Serialize(created.Value), new UTF8Encoding(false));
        File.WriteAllBytes(publicKeyPath, publicKey);
        return new RecoveryRequestFiles(requestPath, publicKeyPath);
    }

    private sealed record AdminProcessResult(
        int ExitCode,
        string Output,
        string Error);

    private sealed record RecoveryRequestFiles(string RequestPath, string PublicKeyPath);

    private sealed class DevQaSigningKeyTrustVerifier(ECDsa key) : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload)
        {
            if (!payload.VendorId.Equals("dobby.dev-qa", StringComparison.Ordinal))
            {
                return PluginTrustVerificationResult.Failure("trust-failed", "Sample smoke only trusts dev/QA artifacts.");
            }

            if (!payload.Algorithm.Equals("ECDSA-P256-SHA256", StringComparison.Ordinal) ||
                !payload.Purpose.Equals(ArtifactSignaturePurposes.PluginPackage, StringComparison.Ordinal) ||
                !payload.Format.Equals(ArtifactSignatureProfile.Format, StringComparison.Ordinal) ||
                !payload.KeyId.Equals(PluginTrustPublicKeyProvider.DevQaPluginPackageKeyId, StringComparison.Ordinal) ||
                !TryDecodeSignature(payload.SignatureValue, out var signature))
            {
                return PluginTrustVerificationResult.Failure("signature-failed", "Sample smoke signature is malformed.");
            }

            return key.VerifyData(
                payload.CanonicalPayload,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                ? PluginTrustVerificationResult.Success()
                : PluginTrustVerificationResult.Failure("signature-failed", "Sample smoke signature verification failed.");
        }

        private static bool TryDecodeSignature(string value, out byte[] signature)
        {
            try
            {
                var padded = value.Replace('-', '+').Replace('_', '/');
                padded = (padded.Length % 4) switch
                {
                    2 => padded + "==",
                    3 => padded + "=",
                    0 => padded,
                    _ => throw new FormatException("Invalid base64url length.")
                };
                signature = Convert.FromBase64String(padded);
                return signature.Length == ArtifactSignatureProfile.P256SignatureLength &&
                    ArtifactSignatureCodec.ToUnpaddedBase64Url(signature).Equals(value, StringComparison.Ordinal);
            }
            catch (FormatException)
            {
                signature = [];
                return false;
            }
        }
    }

    private sealed class RecordingBackgroundOperationReporter : IBackgroundOperationReporter
    {
        private readonly List<BackgroundOperationSnapshot> snapshots = [];
        private readonly IProgress<BackgroundOperationSnapshot> progress;

        public RecordingBackgroundOperationReporter()
        {
            progress = new RecordingProgress(snapshots);
        }

        public IReadOnlyList<BackgroundOperationSnapshot> Snapshots => snapshots;

        public IProgress<BackgroundOperationSnapshot> Progress => progress;

        private sealed class RecordingProgress(List<BackgroundOperationSnapshot> snapshots)
            : IProgress<BackgroundOperationSnapshot>
        {
            public void Report(BackgroundOperationSnapshot value)
            {
                snapshots.Add(value);
            }
        }
    }

    private sealed class RecordingPluginWorkSessionService : IPluginWorkSessionService
    {
        private Guid currentSessionId;

        public List<string> Events { get; } = [];

        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            currentSessionId = Guid.CreateVersion7();
            Events.Add($"open:{ownerId}:{routeKey}:{labelKey}");
            return Result.Success(new PluginWorkSessionHandle(currentSessionId, ownerId, routeKey, labelKey));
        }

        public Result UpdateProgress(Guid sessionId, int percent)
        {
            sessionId.ShouldBe(currentSessionId);
            Events.Add($"progress:{percent}");
            return Result.Success();
        }

        public Result RecordResult(Guid sessionId, string resultKey)
        {
            sessionId.ShouldBe(currentSessionId);
            Events.Add($"result:{resultKey}");
            return Result.Success();
        }

        public Result RecordError(Guid sessionId, string errorSummaryKey)
        {
            sessionId.ShouldBe(currentSessionId);
            Events.Add($"error:{errorSummaryKey}");
            return Result.Success();
        }

        public Result Close(Guid sessionId)
        {
            sessionId.ShouldBe(currentSessionId);
            Events.Add("close");
            return Result.Success();
        }
    }

    private sealed class MutableGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        private readonly HashSet<OperationKey> grants = [];

        public void Grant(string userId, OperationKey key)
        {
            userId.ShouldBe(state.UserId);
            grants.Add(key);
        }

        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return userId.Equals(state.UserId, StringComparison.Ordinal)
                ? Result.Success(state)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(userId.Equals(state.UserId, StringComparison.Ordinal) && grants.Contains(key));
        }
    }

    private sealed class PluginSmokePersistenceFixture : IDisposable
    {
        private PluginSmokePersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            Probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            Probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, Probe);
            Migrator = new HostPersistenceMigrator(Factory, Probe);
            Executor = new HostPersistenceCommandExecutor(Factory);
            TransactionScope = new HostPersistenceTransactionScope(Factory);
            Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        }

        public string Root { get; }

        public ProtectedMaterialAvailabilityProbe Probe { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public HostPersistenceTransactionScope TransactionScope { get; }

        public static PluginSmokePersistenceFixture Create()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-sample-plugin-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new PluginSmokePersistenceFixture(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Non-collectible plugin load contexts can keep the sample DLL locked on Windows.
            }
        }
    }

    private sealed class TempFixture : IDisposable
    {
        private TempFixture(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempFixture Create()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-admin-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempFixture(root);
        }

        public string Path(string fileName)
        {
            return System.IO.Path.Combine(Root, fileName);
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
