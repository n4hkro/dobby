using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Localization;
using Dobby.Messaging;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using Dobby.Shell;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Plugins;

public sealed class PluginIntegrationSuiteTests
{
    [Fact]
    public void Sample_plugin_package_fixture_validates_and_forbidden_variant_rejects()
    {
        using var fixture = SamplePluginPackageFixture.CreateZip();

        var archive = PluginPackageReader.Read(fixture.PackagePath);
        archive.IsSuccess.ShouldBeTrue();

        var validation = PluginPackageValidator.Validate(
            archive.Value,
            new HostVersion(1, 0, 0),
            new AllowAllTrustVerifier());

        validation.IsSuccess.ShouldBeTrue(validation.IsSuccess ? null : validation.Reasons.First().Message);

        using var forbidden = SamplePluginPackageFixture.CreateForbiddenZip();
        var forbiddenArchive = PluginPackageReader.Read(forbidden.PackagePath);
        forbiddenArchive.IsSuccess.ShouldBeTrue();

        var forbiddenValidation = PluginPackageValidator.Validate(
            forbiddenArchive.Value,
            new HostVersion(1, 0, 0),
            new AllowAllTrustVerifier());

        forbiddenValidation.IsSuccess.ShouldBeFalse();
        forbiddenValidation.Reasons.Any(reason => reason.Code == "forbidden-content").ShouldBeTrue();
    }

    [Fact]
    public async Task Installed_sample_plugin_activates_navigation_page_model_loader_isolation_and_background_progress()
    {
        using var fixture = SamplePluginPackageFixture.CreateRuntimeDirectory();
        await using var lifetimes = new PluginLifetimeManager();
        var extensionRegistry = new PluginExtensionPointRegistry();
        var archive = PluginPackageReader.Read(fixture.PackagePath);
        archive.IsSuccess.ShouldBeTrue();
        var packageFingerprint = PluginPackageFingerprint.Compute(archive.Value.Manifest).Fingerprint;
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        stateStore.Snapshot().Returns(Result.Success<IReadOnlyList<InstalledPluginState>>(
        [
            new InstalledPluginState(
                SamplePluginPackageFixture.PluginId,
                SamplePluginPackageFixture.Version,
                packageFingerprint,
                "Sample Plugin",
                "Sample plugin description",
                IsEnabled: true,
                ValidationSummary: "verified",
                RuntimePath: fixture.PackagePath,
                StatusSummary: "Enabled",
                InstalledAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow)
        ]));
        var reporter = new RecordingBackgroundOperationReporter();
        var activationCancellation = new HostPluginActivationCancellation();
        var operationTargets = OperationTargetRegistry.CreateDefault();
        var grants = new MutableGrantReader(new UserPermissionState("managed", IsOwner: false, IsActive: true));
        var operationAuthorization = new OperationAuthorizationPolicy(operationTargets, grants);
        var workSessions = new RecordingPluginWorkSessionService();
        var pluginHostServices = new PluginHostServices(
            new InMemoryEventBus(),
            reporter,
            activationCancellation,
            operationAuthorizationPolicy: operationAuthorization,
            pluginWorkSessions: workSessions);
        var coordinator = new HostPluginRuntimeCoordinator(
            Substitute.For<IHostPersistenceMigrator>(),
            stateStore,
            Substitute.For<IInstalledPluginMetadataCache>(),
            CreateSuccessfulActivationStatusPersistence(),
            Substitute.For<IPluginInstallStagingArea>(),
            Substitute.For<IPluginDataRetentionStore>(),
            operationTargets,
            extensionRegistry,
            lifetimes,
            activationCancellation,
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionRegistry, lifetimes, pluginHostServices)),
            Substitute.For<IPluginCurrentProcessActivationTracker>(),
            new AllowAllTrustVerifier(),
            auditCoordinator: null,
            backgroundOperations: reporter);

        var activation = await coordinator.BeginPostLoginActivation(new HostVersion(1, 0, 0), "managed");

        activation.IsSuccess.ShouldBeTrue(activation.IsSuccess ? null : activation.ErrorCode.ToString());
        activation.Value.Completion.ShouldBe(PluginActivationCompletion.Completed);
        activation.Value.Run.Outcomes.Single().Status.ShouldBe(PluginActivationStatus.Active);
        activation.Value.Run.NavigationContributions.Select(item => item.RouteKey)
            .ShouldBe(["plugin/com.dobby.sample/greet"]);
        reporter.Snapshots.Select(snapshot => snapshot.Status).ShouldContain(BackgroundOperationStatus.Running);
        reporter.Snapshots.Select(snapshot => snapshot.Status).ShouldContain(BackgroundOperationStatus.Completed);

        var shell = ShellViewModel.CreateDefault(new HostStringCatalog());
        shell.ApplyPluginNavigationContributions(
            PluginNavigationContributionMapper.Map(activation.Value.Run.NavigationContributions));
        var pageCoordinator = new PluginPageRouteCoordinator(extensionRegistry, shell, () => "managed");

        var route = await pageCoordinator.ActivateRouteAsync(
            "plugin/com.dobby.sample/greet",
            CancellationToken.None);

        route.IsSuccess.ShouldBeTrue();
        shell.HasActivePluginPage.ShouldBeTrue();
        var pageModel = shell.ActivePluginPageModel.ShouldBeOfType<PluginActionPageModel>();
        pageModel.MessageKey.ShouldBe("sample.home.ready");
        pageModel.Context.UserId.ShouldBe("managed");
        pageModel.Actions.Select(action => action.ActionId).ShouldBe([
            "sample.greet",
            "sample.invalid-input",
            "sample.fault",
            "sample.progress",
            "sample.work-session"
        ]);

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
            snapshot.OwnerId == SamplePluginPackageFixture.PluginId &&
            snapshot.DisplayNameKey == "sample.greet.progress" &&
            snapshot.Status == BackgroundOperationStatus.Running);
        reporter.Snapshots.ShouldContain(snapshot =>
            snapshot.OwnerKind == BackgroundOperationOwnerKind.Plugin &&
            snapshot.OwnerId == SamplePluginPackageFixture.PluginId &&
            snapshot.DisplayNameKey == "sample.greet.progress" &&
            snapshot.Status == BackgroundOperationStatus.Completed);

        var workSession = await pageModel.Actions.Single(action => action.ActionId == "sample.work-session")
            .ExecuteAsync(pageModel.Context, CancellationToken.None);
        workSession.IsSuccess.ShouldBeTrue();
        workSessions.Events.ShouldContain("open:com.dobby.sample:plugin/com.dobby.sample/greet:sample.work-session");
        workSessions.Events.ShouldContain("progress:50");
        workSessions.Events.ShouldContain("result:sample.work-session.completed");
        reporter.Snapshots.ShouldContain(snapshot =>
            snapshot.OwnerKind == BackgroundOperationOwnerKind.Plugin &&
            snapshot.OwnerId == SamplePluginPackageFixture.PluginId &&
            snapshot.Status == BackgroundOperationStatus.Completed);
        var pluginPage = extensionRegistry.GetRegistrations<IPluginPage>().Single().Instance;
        var loadContext = AssemblyLoadContext.GetLoadContext(pluginPage.GetType().Assembly);
        loadContext.ShouldNotBeNull();
        loadContext.ShouldNotBeSameAs(AssemblyLoadContext.Default);
        loadContext!.Name.ShouldStartWith("Dobby.Plugin:com.dobby.sample");
    }

    [Fact]
    public void Plugin_load_context_resolves_private_dependency_beside_plugin_entry()
    {
        using var fixture = PrivateDependencyPluginFixture.Create();
        var context = new PluginLoadContext("com.dobby.private-dependency", fixture.EntryAssemblyPath);
        var entryAssembly = context.LoadFromAssemblyPath(fixture.EntryAssemblyPath);
        var probe = entryAssembly.GetType("Plugin.Entry.EntryProbe", throwOnError: true)!;
        var probeMethod = probe.GetMethod("Probe", BindingFlags.Public | BindingFlags.Static)!;

        var result = (string)probeMethod.Invoke(null, null)!;

        result.ShouldBe("private-helper");
        var helperAssembly = context.Assemblies.Single(assembly =>
            assembly.GetName().Name == "Plugin.PrivateDependency");
        helperAssembly.Location.ShouldStartWith(fixture.RuntimePath);
        AssemblyLoadContext.GetLoadContext(helperAssembly).ShouldBeSameAs(context);
        AssemblyLoadContext.Default.Assemblies
            .Select(assembly => assembly.GetName().Name)
            .ShouldNotContain("Plugin.PrivateDependency");
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

    private static IHostPluginActivationStatusPersistence CreateSuccessfulActivationStatusPersistence()
    {
        var persistence = Substitute.For<IHostPluginActivationStatusPersistence>();
        persistence
            .Persist(Arg.Any<IReadOnlyList<InstalledPluginState>>(), Arg.Any<IReadOnlyList<InstalledPluginState>>())
            .Returns(Result.Success());
        return persistence;
    }

    private sealed class AllowAllTrustVerifier : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Success();
    }

    private sealed class MutableGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        private readonly HashSet<string> grants = new(StringComparer.Ordinal);

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
            return Result.Success(grants.Contains($"{userId}:{key}"));
        }
    }

    private sealed class RecordingPluginWorkSessionService : IPluginWorkSessionService
    {
        private Guid currentSessionId;

        public List<string> Events { get; } = [];

        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            currentSessionId = Guid.NewGuid();
            Events.Add($"open:{ownerId}:{routeKey}:{labelKey}");
            return Result.Success(new PluginWorkSessionHandle(currentSessionId, ownerId, routeKey, labelKey));
        }

        public Result UpdateProgress(Guid sessionId, int percent)
        {
            sessionId.ShouldBe(currentSessionId);
            Events.Add($"progress:{percent}");
            return Result.Success();
        }

        public Result RecordResult(Guid sessionId, string resultSummaryKey)
        {
            sessionId.ShouldBe(currentSessionId);
            Events.Add($"result:{resultSummaryKey}");
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

    private sealed class PrivateDependencyPluginFixture : IDisposable
    {
        private readonly string root;

        private PrivateDependencyPluginFixture(string root, string runtimePath, string entryAssemblyPath)
        {
            this.root = root;
            RuntimePath = runtimePath;
            EntryAssemblyPath = entryAssemblyPath;
        }

        public string RuntimePath { get; }

        public string EntryAssemblyPath { get; }

        public static PrivateDependencyPluginFixture Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "dobby-private-dependency-plugin-" + Guid.NewGuid().ToString("N"));
            var helperProject = Path.Combine(root, "Plugin.PrivateDependency");
            var entryProject = Path.Combine(root, "Plugin.Entry");
            var runtimePath = Path.Combine(root, "runtime");
            Directory.CreateDirectory(helperProject);
            Directory.CreateDirectory(entryProject);
            Directory.CreateDirectory(runtimePath);

            File.WriteAllText(
                Path.Combine(helperProject, "Plugin.PrivateDependency.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(
                Path.Combine(helperProject, "Helper.cs"),
                """
                namespace Plugin.PrivateDependency;

                public static class Helper
                {
                    public static string Message() => "private-helper";
                }
                """);
            File.WriteAllText(
                Path.Combine(entryProject, "Plugin.Entry.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="..\Plugin.PrivateDependency\Plugin.PrivateDependency.csproj" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(
                Path.Combine(entryProject, "EntryProbe.cs"),
                """
                using Plugin.PrivateDependency;

                namespace Plugin.Entry;

                public static class EntryProbe
                {
                    public static string Probe() => Helper.Message();
                }
                """);

            RunDotnetBuild(Path.Combine(entryProject, "Plugin.Entry.csproj"), runtimePath);
            return new PrivateDependencyPluginFixture(
                root,
                runtimePath,
                Path.Combine(runtimePath, "Plugin.Entry.dll"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static void RunDotnetBuild(string projectPath, string outputPath)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("Debug");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("--nologo");

            using var process = Process.Start(startInfo) ??
                throw new InvalidOperationException("dotnet build process could not be started.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(output + Environment.NewLine + error);
            }
        }
    }
}

internal sealed class SamplePluginPackageFixture : IDisposable
{
    public const string PluginId = "com.dobby.sample";
    public const string Version = "1.0.0";

    private const string EntryAssembly = "bin/plugins/Dobby.Sample.Plugin.dll";
    private readonly string root;

    private SamplePluginPackageFixture(string root, string packagePath)
    {
        this.root = root;
        PackagePath = packagePath;
    }

    public string PackagePath { get; }

    public static SamplePluginPackageFixture CreateZip()
    {
        var root = CreateRoot();
        var packagePath = Path.Combine(root, "sample.dobbyplugin");
        var sampleAssembly = File.ReadAllBytes(FindSamplePluginAssembly());
        var manifestJson = CreateManifestJson(
            [new PackageFile(EntryAssembly, Sha256(sampleAssembly))],
            navigationTarget: "top-toolbar");

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
        WriteZipEntry(archive, EntryAssembly, sampleAssembly);
        return new SamplePluginPackageFixture(root, packagePath);
    }

    public static SamplePluginPackageFixture CreateForbiddenZip()
    {
        var root = CreateRoot();
        var packagePath = Path.Combine(root, "forbidden.dobbyplugin");
        var forbiddenBytes = Encoding.UTF8.GetBytes("customer data");
        const string forbiddenPath = "customer-data/export.json";
        var manifestJson = CreateManifestJson(
            [new PackageFile(forbiddenPath, Sha256(forbiddenBytes))],
            navigationTarget: "top-toolbar",
            entryAssembly: forbiddenPath);

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        WriteZipEntry(archive, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
        WriteZipEntry(archive, forbiddenPath, forbiddenBytes);
        return new SamplePluginPackageFixture(root, packagePath);
    }

    public static SamplePluginPackageFixture CreateRuntimeDirectory()
    {
        var root = CreateRoot();
        var runtimePath = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtimePath);
        var sampleAssembly = File.ReadAllBytes(FindSamplePluginAssembly());
        var manifestJson = CreateManifestJson(
            [new PackageFile(EntryAssembly, Sha256(sampleAssembly))],
            navigationTarget: "top-toolbar");
        File.WriteAllText(Path.Combine(runtimePath, PluginPackageReader.ManifestFileName), manifestJson, Encoding.UTF8);
        var entryPath = Path.Combine(runtimePath, EntryAssembly.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(entryPath)!);
        File.WriteAllBytes(entryPath, sampleAssembly);
        return new SamplePluginPackageFixture(root, runtimePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Non-collectible plugin load contexts can keep the sample DLL locked on Windows.
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "dobby-plugin-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteZipEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string CreateManifestJson(
        IReadOnlyList<PackageFile> files,
        string navigationTarget,
        string entryAssembly = EntryAssembly)
    {
        var filesJson = string.Join(
            ",",
            files.Select(file => $$"""{"path":"{{file.Path}}","sha256":"{{file.Sha256}}"}"""));
        return
            "{\"schemaVersion\":2,\"purpose\":\"dobby.plugin-package\",\"pluginId\":\"" + PluginId +
            "\",\"version\":\"" + Version +
            "\",\"vendorId\":\"dobby.vendor\",\"hostApiVersion\":\"2.0\",\"minHostVersion\":\"1.0.0\",\"entryAssembly\":\"" + entryAssembly +
            "\",\"displayName\":\"Sample Plugin\",\"description\":\"Sample plugin description\",\"dependsOn\":[],\"capabilities\":[{\"name\":\"plugin-page\",\"scope\":\"plugin-page\"}],\"operationTargets\":[{\"id\":\"" + PluginId +
            ".greet\",\"verbs\":[\"execute\"],\"labelKey\":\"sample.greet.label\"}],\"extensionPoints\":[{\"id\":\"sample.greet.page\",\"target\":\"" + navigationTarget +
            "\",\"routeKey\":\"greet\",\"labelKey\":\"sample.nav.greet\",\"group\":\"default\",\"weight\":100}],\"files\":[" + filesJson +
            "],\"signature\":{\"purpose\":\"dobby.plugin-package\",\"algorithm\":\"ECDSA-P256-SHA256\",\"format\":\"IeeeP1363FixedFieldConcatenation\",\"keyId\":\"plugin-prod-2026-01\",\"value\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}}";
    }

    private static string FindSamplePluginAssembly()
    {
        var root = FindRepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", BuildConfiguration(), "net10.0", "Dobby.Sample.Plugin.dll"),
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Release", "net10.0", "Dobby.Sample.Plugin.dll"),
            Path.Combine(root, "samples", "Dobby.Sample.Plugin", "bin", "Debug", "net10.0", "Dobby.Sample.Plugin.dll")
        };

        var match = candidates.FirstOrDefault(File.Exists);
        return match ?? throw new InvalidOperationException("Sample plugin assembly was not built.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dobby.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record PackageFile(string Path, string Sha256);
}
