using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Audit;
using Dobby.Admin.Commands;
using Dobby.Configuration;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Admin;

public sealed class AdminCommandHostTests
{
    private const string DevQaSigningKeyPathVariable = "DOBBY_ADMIN_SIGNING_DEV_QA_KEY_PATH";
    private const string ProductionSigningKeyPathVariable = "DOBBY_ADMIN_SIGNING_PRODUCTION_KEY_PATH";

    [Theory]
    [MemberData(nameof(InvalidOptionShapes))]
    public void Run_rejects_unknown_foreign_repeated_and_missing_option_shapes(string[] args)
    {
        var result = AdminCommandHost.Run(args);

        result.ExitCode.ShouldBe(64);
        result.StandardError.ShouldContain("option contract is not approved");
        result.StandardError.ShouldNotContain(" at ");
    }

    [Fact]
    public void Run_maps_profile_mismatch_invalid_timestamps_revision_and_output_conflicts_to_validation_failure()
    {
        using var fixture = TempFixture.Create();
        var output = fixture.Path("artifact.dobbyplugin");
        File.WriteAllText(output, "already exists");

        AdminCommandHost.Run([
            "--cli", "sign-plugin-package",
            "--input-path", fixture.Root,
            "--signing-profile", "dev-qa",
            "--trust-profile", "production",
            "--output-path", fixture.Path("out.dobbyplugin")
        ]).StandardError.ShouldContain("profile-mismatch");

        AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", fixture.Path("package.dobbyplugin"),
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", fixture.Path("device.json"),
            "--valid-from", "2026-06-18T00:00:00+00:00",
            "--valid-until", "2026-07-18T00:00:00Z",
            "--revision", "1",
            "--output-path", fixture.Path("license.dobbylicense")
        ]).StandardError.ShouldContain("invalid-timestamp");

        AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", fixture.Path("package.dobbyplugin"),
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", fixture.Path("device.json"),
            "--valid-from", "2026-06-18T00:00:00Z",
            "--valid-until", "2026-07-18T00:00:00Z",
            "--revision", "0",
            "--output-path", fixture.Path("license.dobbylicense")
        ]).StandardError.ShouldContain("invalid-revision");

        var conflict = AdminCommandHost.Run([
            "--cli", "sign-plugin-package",
            "--input-path", fixture.Root,
            "--signing-profile", "dev-qa",
            "--output-path", output
        ]);

        conflict.ExitCode.ShouldBe(65);
        conflict.StandardError.ShouldContain("output-conflict");
        conflict.StandardError.ShouldNotContain(fixture.Root);
    }

    [Fact]
    public void Run_returns_unavailable_signing_material_for_known_profile_without_default_material()
    {
        using var environment = SigningKeyEnvironmentScope.Clear();
        using var fixture = TempFixture.Create();
        File.WriteAllText(fixture.Path("request.dobbyrecoveryrequest"), RecoveryRequestJson());

        var result = AdminCommandHost.Run([
            "--cli", "create-recovery-response",
            "--request-path", fixture.Path("request.dobbyrecoveryrequest"),
            "--signing-profile", "dev-qa",
            "--output-path", fixture.Path("response.dobbyrecovery")
        ]);

        result.ExitCode.ShouldBe(66);
        result.StandardError.ShouldContain("signing-material-unavailable");
        result.StandardError.ShouldNotContain(fixture.Root);
    }

    [Fact]
    public void Run_uses_default_environment_signing_material_for_recovery_response()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var environment = SigningKeyEnvironmentScope.ForDevQa(fixture, key);
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T10:00:00Z"));
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        File.WriteAllText(requestPath, RecoveryRequestJson());

        var result = AdminCommandHost.Run([
            "--cli", "create-recovery-response",
            "--request-path", requestPath,
            "--signing-profile", "dev-qa",
            "--output-path", outputPath
        ], timeProvider: time);

        result.ExitCode.ShouldBe(0, result.StandardError);
        File.Exists(outputPath).ShouldBeTrue();
        RecoveryResponseImporter.ReadEnvelope(File.ReadAllText(outputPath)).Signature.ShouldContain("=");
    }

    [Fact]
    public void Sign_plugin_package_uses_default_environment_material_and_refreshes_candidate_hashes()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var environment = SigningKeyEnvironmentScope.ForDevQa(fixture, key);
        var publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        var unsignedPackagePath = fixture.Path("unsigned-stale.dobbyplugin");
        var signedPackagePath = fixture.Path("signed.dobbyplugin");
        CreateUnsignedSamplePackage(unsignedPackagePath, staleAssemblyHash: true);

        var result = AdminCommandHost.Run([
            "--cli", "sign-plugin-package",
            "--input-path", unsignedPackagePath,
            "--signing-profile", "dev-qa",
            "--output-path", signedPackagePath
        ]);

        result.ExitCode.ShouldBe(0, result.StandardError);
        var archive = PluginPackageReader.Read(signedPackagePath);
        archive.IsSuccess.ShouldBeTrue(archive.ErrorCode?.ToString());
        archive.Value.Manifest.Files
            .Single(file => file.Path == "bin/plugins/Dobby.Sample.Plugin.dll")
            .Sha256.ShouldNotBe(new string('0', 64));
        var validation = PluginPackageValidator.Validate(
            archive.Value,
            new HostVersion(1, 0, 0),
            new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
                DobbyPluginTrustProfile.DevQa,
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [PluginTrustPublicKeyProvider.DevQaResourceName] = publicKeyPem
                })));

        validation.IsSuccess.ShouldBeTrue(validation.IsSuccess ? null : validation.Reasons.First().Message);
    }

    [Fact]
    public void Create_plugin_license_uses_default_environment_signing_material()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var environment = SigningKeyEnvironmentScope.ForDevQa(fixture, key);
        var publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        var packagePath = fixture.Path("package.dobbyplugin");
        var bindingPath = fixture.Path("device.json");
        var outputPath = fixture.Path("sample.dobbylicense");
        File.WriteAllText(packagePath, "package-bytes");
        File.WriteAllText(bindingPath, """{"deviceFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");

        var result = AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", packagePath,
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", bindingPath,
            "--valid-from", "2026-06-20T00:00:00Z",
            "--valid-until", "2026-07-20T00:00:00Z",
            "--revision", "3",
            "--output-path", outputPath
        ]);

        result.ExitCode.ShouldBe(0, result.StandardError);
        var parsed = LicenseArtifactParser.Parse(Path.GetFileName(outputPath), File.ReadAllBytes(outputPath));
        parsed.IsSuccess.ShouldBeTrue(parsed.ReasonCode);
        new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
                DobbyPluginTrustProfile.DevQa,
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [PluginTrustPublicKeyProvider.DevQaResourceName] = publicKeyPem
                }))
            .Verify(parsed.Artifact!.Signature, parsed.Artifact.CanonicalPayload)
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Sign_plugin_package_writes_validator_compatible_package_with_redacted_report()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        var unsignedPackagePath = fixture.Path("unsigned.dobbyplugin");
        var signedPackagePath = fixture.Path("signed.dobbyplugin");
        var reportPath = fixture.Path("signed.report.json");
        CreateUnsignedSamplePackage(unsignedPackagePath);

        var result = AdminCommandHost.Run([
            "--cli", "sign-plugin-package",
            "--input-path", unsignedPackagePath,
            "--signing-profile", "dev-qa",
            "--output-path", signedPackagePath,
            "--report-path", reportPath
        ], new InMemorySigningMaterialProvider("dev-qa", key));

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("sign-plugin-package");
        result.StandardOutput.ShouldNotContain(fixture.Root);
        File.Exists(signedPackagePath).ShouldBeTrue();
        File.Exists(reportPath).ShouldBeTrue();
        File.ReadAllText(reportPath).ShouldNotContain(fixture.Root);

        var archive = PluginPackageReader.Read(signedPackagePath);
        archive.IsSuccess.ShouldBeTrue(archive.ErrorCode?.ToString());
        archive.Value.Manifest.Signature.Value.ShouldNotBe("AQ");
        var validation = PluginPackageValidator.Validate(
            archive.Value,
            new HostVersion(1, 0, 0),
            new PluginTrustVerifier(new PluginTrustPublicKeyProvider(
                DobbyPluginTrustProfile.DevQa,
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [PluginTrustPublicKeyProvider.DevQaResourceName] = publicKeyPem
                })));

        validation.IsSuccess.ShouldBeTrue(validation.IsSuccess ? null : validation.Reasons.First().Message);
    }

    [Fact]
    public void Create_recovery_response_writes_host_compatible_envelope_through_concrete_verifier()
    {
        if (!IsDevQaTrustProfileBuild())
        {
            return;
        }

        using var fixture = TempFixture.Create();
        using var environment = SigningKeyEnvironmentScope.ForDevQaPath(ApprovedDevQaBootstrapSigningKeyPath());
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T10:00:00Z"));
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        var reportPath = fixture.Path("response.report.json");
        File.WriteAllText(requestPath, RecoveryRequestJson());

        var result = AdminCommandHost.Run([
            "--cli", "create-recovery-response",
            "--request-path", requestPath,
            "--signing-profile", "dev-qa",
            "--output-path", outputPath,
            "--report-path", reportPath
        ], timeProvider: time);

        result.ExitCode.ShouldBe(0, result.StandardError);
        result.StandardOutput.ShouldContain("create-recovery-response");
        result.StandardOutput.ShouldNotContain(fixture.Root);
        File.Exists(outputPath).ShouldBeTrue();
        File.Exists(reportPath).ShouldBeTrue();

        var responseJson = File.ReadAllText(outputPath);
        responseJson.ShouldNotContain("targetUserId");
        File.ReadAllText(reportPath).ShouldNotContain("machine", Case.Insensitive);
        var envelope = RecoveryResponseImporter.ReadEnvelope(responseJson);
        envelope.Signature.ShouldContain("=");
        envelope.Signature.ShouldNotContain("-");
        envelope.Signature.ShouldNotContain("_");

        var importer = new RecoveryResponseImporter(
            new VendorRecoveryPublicKey(new PluginTrustPublicKeyProvider(DobbyPluginTrustProfile.DevQa)),
            new StaticDeviceFingerprint(new DeviceFingerprint("machine", "cpu", "board", "disk", "mac")));

        var verify = importer.Verify(envelope, time.GetUtcNow());

        verify.IsSuccess.ShouldBeTrue(verify.IsSuccess ? null : verify.ErrorCode.ToString());
    }

    [Fact]
    public void Create_recovery_response_verifies_through_host_import_coordinator_with_concrete_verifier()
    {
        if (!IsDevQaTrustProfileBuild())
        {
            return;
        }

        using var fixture = TempFixture.Create();
        using var environment = SigningKeyEnvironmentScope.ForDevQaPath(ApprovedDevQaBootstrapSigningKeyPath());
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T10:00:00Z"));
        var requestPath = fixture.Path("request.dobbyrecoveryrequest");
        var outputPath = fixture.Path("response.dobbyrecovery");
        File.WriteAllText(requestPath, RecoveryRequestJson());
        var result = AdminCommandHost.Run([
            "--cli", "create-recovery-response",
            "--request-path", requestPath,
            "--signing-profile", "dev-qa",
            "--output-path", outputPath
        ], timeProvider: time);
        result.ExitCode.ShouldBe(0, result.StandardError);
        var importer = new RecoveryResponseImporter(
            new VendorRecoveryPublicKey(new PluginTrustPublicKeyProvider(DobbyPluginTrustProfile.DevQa)),
            new StaticDeviceFingerprint(new DeviceFingerprint("machine", "cpu", "board", "disk", "mac")));
        var coordinator = new RecoveryResponseImportCoordinator(
            new NoopTransactionScope(),
            new RecoveryVerificationExecutor(),
            importer,
            new NoopProtectedDataProtector(),
            new KdfPolicyResolver(new AuthOptions { PasswordKdfMode = "Pbkdf2Sha256" }),
            new NoopHostTransactionAuditTrail(),
            time);

        var challenge = coordinator.VerifyFile(outputPath);

        challenge.IsSuccess.ShouldBeTrue(challenge.ErrorCode?.ToString());
        challenge.Value.Payload.RequestId.ShouldBe("request-1");
    }

    [Fact]
    public void Create_plugin_license_writes_importable_license_with_injected_material()
    {
        using var fixture = TempFixture.Create();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKeyPem = EncodePem(key.ExportSubjectPublicKeyInfo());
        var packagePath = fixture.Path("package.dobbyplugin");
        var bindingPath = fixture.Path("device.json");
        var outputPath = fixture.Path("sample.dobbylicense");
        File.WriteAllText(packagePath, "package-bytes");
        File.WriteAllText(bindingPath, """{"deviceFingerprint":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");

        var result = AdminCommandHost.Run([
            "--cli", "create-plugin-license",
            "--plugin-package-path", packagePath,
            "--signing-profile", "dev-qa",
            "--customer-id", "customer",
            "--deployment-id", "deployment",
            "--device-binding-fixture-path", bindingPath,
            "--valid-from", "2026-06-20T00:00:00Z",
            "--valid-until", "2026-07-20T00:00:00Z",
            "--revision", "3",
            "--output-path", outputPath
        ], new InMemorySigningMaterialProvider("dev-qa", key));

        result.ExitCode.ShouldBe(0, result.StandardError);
        var report = File.ReadAllText(outputPath + ".report.json");
        report.ShouldNotContain("\"customer\"");
        report.ShouldNotContain("\"deployment\"");
        report.ShouldContain("customerIdHash");
        report.ShouldContain("deploymentIdHash");
        var parsed = LicenseArtifactParser.Parse(Path.GetFileName(outputPath), File.ReadAllBytes(outputPath));
        parsed.IsSuccess.ShouldBeTrue(parsed.ReasonCode);
        LicensePayloadValidator.Validate(parsed.Artifact!).IsSuccess.ShouldBeTrue();
        new LicenseSignatureVerifier(new PluginTrustPublicKeyProvider(
                DobbyPluginTrustProfile.DevQa,
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    [PluginTrustPublicKeyProvider.DevQaResourceName] = publicKeyPem
                }))
            .Verify(parsed.Artifact!.Signature, parsed.Artifact.CanonicalPayload)
            .IsSuccess.ShouldBeTrue();
    }

    public static IEnumerable<object[]> InvalidOptionShapes()
    {
        yield return [new[] { "--cli", "create-recovery-response", "--unapproved-option" }];
        yield return [new[] { "--cli", "create-recovery-response", "--request-path" }];
        yield return [new[] { "--cli", "create-recovery-response", "--request-path", "a", "--request-path", "b", "--signing-profile", "dev-qa", "--output-path", "out" }];
        yield return [new[] { "--cli", "create-recovery-response", "--plugin-package-path", "pkg", "--signing-profile", "dev-qa", "--output-path", "out" }];
        yield return [new[] { "--cli", "create-recovery-response", "--request-path", "a", "--signing-profile", "dev-qa", "--output-path", "out", "--overwrite", "true" }];
    }

    private static bool IsDevQaTrustProfileBuild()
    {
        var profile = typeof(PluginTrustPublicKeyProvider).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "DobbyPluginTrustProfile")
            .Value;
        return string.Equals(profile, "DevQa", StringComparison.Ordinal);
    }

    private static string ApprovedDevQaBootstrapSigningKeyPath()
    {
        var path = Environment.GetEnvironmentVariable(DevQaSigningKeyPathVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dobby-signing-keys",
                "sprint8-bootstrap",
                "dev-qa-bootstrap-private-key.pem");
        }

        File.Exists(path).ShouldBeTrue("Approved dev/QA bootstrap signing material is required for DevQa-profile recovery proof.");
        return path;
    }

    private static string RecoveryRequestJson()
    {
        return """
            {
              "type": "dobby.owner-recovery-request.v1",
              "dobbyVersion": "1.0.0",
              "requestedAt": "2026-06-20T09:00:00Z",
              "deviceIdentity": {
                "machineGuid": "machine",
                "cpuId": "cpu",
                "motherboardSerial": "board",
                "diskSerial": "disk",
                "macAddress": "mac"
              },
              "deviceDisplayName": "Owner laptop",
              "requestId": "request-1"
            }
            """;
    }

    private static byte[] EncodePem(byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        return Encoding.UTF8.GetBytes($"-----BEGIN PUBLIC KEY-----\n{base64}\n-----END PUBLIC KEY-----\n");
    }

    private static void CreateUnsignedSamplePackage(string packagePath, bool staleAssemblyHash = false)
    {
        var assemblyPath = FindSamplePluginAssembly();
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var assemblyHash = staleAssemblyHash ? new string('0', 64) : Sha256(assemblyBytes);
        var manifestJson =
            "{\"pluginId\":\"com.dobby.sample\"," +
            "\"version\":\"1.0.0\"," +
            "\"vendorId\":\"dobby.dev-qa\"," +
            "\"minHostVersion\":\"1.0.0\"," +
            "\"entryAssembly\":\"bin/plugins/Dobby.Sample.Plugin.dll\"," +
            "\"displayName\":\"Dobby Sample\"," +
            "\"description\":\"Dev and QA plugin verification artifact for Dobby host/plugin boundaries.\"," +
            "\"dependsOn\":[]," +
            "\"capabilities\":[{\"name\":\"plugin-page\",\"scope\":\"plugin-page\"}]," +
            "\"operationTargets\":[{\"id\":\"com.dobby.sample.greet\",\"verbs\":[\"execute\"],\"labelKey\":\"sample.greet.label\"}]," +
            "\"extensionPoints\":[{\"id\":\"sample.greet.page\",\"target\":\"sidebar\",\"routeKey\":\"greet\",\"labelKey\":\"sample.nav.greet\",\"group\":\"default\",\"weight\":100}]," +
            "\"files\":[{\"path\":\"bin/plugins/Dobby.Sample.Plugin.dll\",\"sha256\":\"" + assemblyHash + "\"}]," +
            "\"signature\":{\"algorithm\":\"ECDSA-P256-SHA256\",\"keyVersion\":1,\"value\":\"AQ\"}}";

        using var stream = File.Create(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        WriteZipEntry(archive, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
        WriteZipEntry(archive, "bin/plugins/Dobby.Sample.Plugin.dll", assemblyBytes);
    }

    private static void WriteZipEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string FindSamplePluginAssembly()
    {
        var root = RepoRoot().FullName;
        var candidates = new[]
        {
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Release", "net10.0", "Dobby.Sample.Plugin.dll"),
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Debug", "net10.0", "Dobby.Sample.Plugin.dll")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("Sample plugin assembly must be built before admin signing verification.");
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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

    private sealed class InMemorySigningMaterialProvider(string profile, ECDsa key) : IAdminSigningMaterialProvider
    {
        public AdminSigningMaterialResult Resolve(string signingProfile)
        {
            return signingProfile.Equals(profile, StringComparison.Ordinal)
                ? AdminSigningMaterialResult.Success(new AdminSigningMaterial(signingProfile, key))
                : AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
        }
    }

    private sealed class StaticDeviceFingerprint(DeviceFingerprint fingerprint) : IDeviceFingerprintSource
    {
        public Result<DeviceFingerprint> Current() => Result.Success(fingerprint);
    }

    private sealed class RecoveryVerificationExecutor : IHostPersistenceCommandExecutor
    {
        public int Execute(string sql, object? parameters = null)
        {
            return 0;
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            if (typeof(T) == typeof(string) && sql.Contains("FROM Users", StringComparison.Ordinal))
            {
                return (IReadOnlyList<T>)(object)new[] { "owner-id" };
            }

            return [];
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            if (typeof(T) == typeof(int) && sql.Contains("ConsumedRecoveryNonces", StringComparison.Ordinal))
            {
                return (T)(object)0;
            }

            return default!;
        }
    }

    private sealed class NoopTransactionScope : IHostPersistenceTransactionScope
    {
        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            action(new RecoveryVerificationExecutor());
        }
    }

    private sealed class NoopProtectedDataProtector : IProtectedDataProtector
    {
        public byte[] Protect(byte[] plaintext, byte[] entropy) => plaintext;

        public byte[] Unprotect(byte[] protectedBlob, byte[] entropy) => protectedBlob;
    }

    private sealed class NoopHostTransactionAuditTrail : IHostTransactionAuditTrail
    {
        public void AppendInScope(IHostPersistenceCommandExecutor executor, AuditEvent auditEvent)
        {
        }
    }

    private sealed class SigningKeyEnvironmentScope : IDisposable
    {
        private readonly string? previousDevQa;
        private readonly string? previousProduction;

        private SigningKeyEnvironmentScope()
        {
            previousDevQa = Environment.GetEnvironmentVariable(DevQaSigningKeyPathVariable);
            previousProduction = Environment.GetEnvironmentVariable(ProductionSigningKeyPathVariable);
        }

        public static SigningKeyEnvironmentScope Clear()
        {
            var scope = new SigningKeyEnvironmentScope();
            Environment.SetEnvironmentVariable(DevQaSigningKeyPathVariable, null);
            Environment.SetEnvironmentVariable(ProductionSigningKeyPathVariable, null);
            return scope;
        }

        public static SigningKeyEnvironmentScope ForDevQa(TempFixture fixture, ECDsa key)
        {
            var scope = Clear();
            var keyPath = fixture.Path("dev-qa-private-key.pem");
            File.WriteAllText(keyPath, key.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
            Environment.SetEnvironmentVariable(DevQaSigningKeyPathVariable, keyPath);
            return scope;
        }

        public static SigningKeyEnvironmentScope ForDevQaPath(string keyPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);

            var scope = Clear();
            Environment.SetEnvironmentVariable(DevQaSigningKeyPathVariable, keyPath);
            return scope;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(DevQaSigningKeyPathVariable, previousDevQa);
            Environment.SetEnvironmentVariable(ProductionSigningKeyPathVariable, previousProduction);
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
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-admin-" + Guid.NewGuid().ToString("N"));
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
