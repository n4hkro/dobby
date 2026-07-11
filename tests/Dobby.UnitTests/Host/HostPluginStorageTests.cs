using System.Text;
using System.Data.Common;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.PluginFramework.Activation;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostPluginStorageTests
{
    [Fact]
    public async Task Default_plugin_host_services_do_not_expose_writable_plugin_storage()
    {
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            PluginHostServices.CreateDefault());
        var prepared = bridge.PrepareRegistration("plugin.default", new StorageAwareModule());

        prepared.IsSuccess.ShouldBeTrue();
        var commit = await bridge.CommitPreparedAsync(prepared.Value, CancellationToken.None);

        commit.IsSuccess.ShouldBeFalse();
        commit.ErrorCode.ShouldBe(ErrorCode.PluginInternalError);
    }

    [Fact]
    public async Task Plugin_service_bridge_injects_plugin_scoped_storage()
    {
        using var fixture = HostPluginStorageFixture.Create();
        await using var lifetimes = new PluginLifetimeManager();
        await using var bridge = new PluginServiceRegistrationBridge(
            new PluginExtensionPointRegistry(),
            lifetimes,
            new PluginHostServices(
                new NoopEventBus(),
                new NoopBackgroundOperationReporter(),
                new NoopBackgroundOperationCancellation(),
                pluginStorageFactory: pluginId => new HostPluginStorage(
                    pluginId,
                    fixture.Executor,
                    fixture.TransactionScope,
                    () => "user-a",
                    FullAccessMutationGate())));
        var sharedKey = PluginStorageKey.Create("settings/theme").Value;
        var sharedValue = new PluginStorageValue("blue"u8.ToArray(), "text/plain", 3);

        var alphaPrepared = bridge.PrepareRegistration("plugin.alpha", new StorageAwareModule());
        var betaPrepared = bridge.PrepareRegistration("plugin.beta", new StorageAwareModule());

        alphaPrepared.IsSuccess.ShouldBeTrue();
        betaPrepared.IsSuccess.ShouldBeTrue();
        (await bridge.CommitPreparedAsync(alphaPrepared.Value, CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await bridge.CommitPreparedAsync(betaPrepared.Value, CancellationToken.None)).IsSuccess.ShouldBeTrue();

        var alphaPage = alphaPrepared.Value.Resolve<StorageAwarePage>();
        var betaPage = betaPrepared.Value.Resolve<StorageAwarePage>();

        alphaPage.Storage.ShouldNotBeNull();
        betaPage.Storage.ShouldNotBeNull();
        alphaPage.Storage.ShouldNotBeSameAs(betaPage.Storage);
        (await alphaPage.Storage.SharedDomainStorage.SetAsync(sharedKey, sharedValue)).IsSuccess.ShouldBeTrue();

        var alphaRead = await alphaPage.Storage.SharedDomainStorage.GetAsync(sharedKey);
        var betaRead = await betaPage.Storage.SharedDomainStorage.GetAsync(sharedKey);

        alphaRead.IsSuccess.ShouldBeTrue();
        alphaRead.Value.ShouldNotBeNull();
        alphaRead.Value.Payload.ToArray().ShouldBe(sharedValue.Payload.ToArray());
        betaRead.IsSuccess.ShouldBeTrue();
        betaRead.Value.ShouldBeNull();
    }

    [Fact]
    public async Task Shared_and_user_compartments_round_trip_and_isolate_values()
    {
        using var fixture = HostPluginStorageFixture.Create();
        var userAStorage = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            FullAccessMutationGate());
        var userBStorage = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-b",
            FullAccessMutationGate());
        var reloadedUserAStorage = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            FullAccessMutationGate());
        var otherPluginStorage = new HostPluginStorage(
            "plugin.other",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            FullAccessMutationGate());
        var sharedKey = PluginStorageKey.Create("prefs/layout").Value;
        var userKey = PluginStorageKey.Create("drafts/current").Value;
        var sharedValue = new PluginStorageValue(new byte[] { 0x10, 0x20, 0x30 }, "application/octet-stream", 7);
        var userValue = PluginStorageValue.FromUtf8Json("""{"value":"a"}""", schemaVersion: 4);

        (await userAStorage.SharedDomainStorage.SetAsync(sharedKey, sharedValue)).IsSuccess.ShouldBeTrue();
        (await userAStorage.UserScopedStorage.SetAsync(userKey, userValue)).IsSuccess.ShouldBeTrue();
        (await userBStorage.UserScopedStorage.SetAsync(userKey, PluginStorageValue.FromUtf8Json("""{"value":"b"}""", schemaVersion: 5))).IsSuccess.ShouldBeTrue();

        var sharedRead = await userAStorage.SharedDomainStorage.GetAsync(sharedKey);
        var userARead = await userAStorage.UserScopedStorage.GetAsync(userKey);
        var userBRead = await userBStorage.UserScopedStorage.GetAsync(userKey);
        var reloadedUserARead = await reloadedUserAStorage.UserScopedStorage.GetAsync(userKey);
        var otherPluginRead = await otherPluginStorage.SharedDomainStorage.GetAsync(sharedKey);
        var userAKeys = await userAStorage.UserScopedStorage.ListKeysAsync(PluginStorageKeyPrefix.Create("drafts").Value);
        var sharedExists = await userAStorage.SharedDomainStorage.ExistsAsync(sharedKey);
        var deleted = await userAStorage.UserScopedStorage.DeleteAsync(userKey);
        var deletedAgain = await userAStorage.UserScopedStorage.DeleteAsync(userKey);
        var invalidSet = await userAStorage.SharedDomainStorage.SetAsync(
            new PluginStorageKey("../escape"),
            sharedValue);
        var invalidGet = await userAStorage.SharedDomainStorage.GetAsync(new PluginStorageKey("host:reserved"));
        var invalidList = await userAStorage.SharedDomainStorage.ListKeysAsync(new PluginStorageKeyPrefix("../"));

        sharedRead.IsSuccess.ShouldBeTrue();
        sharedRead.Value.ShouldNotBeNull();
        sharedRead.Value.Payload.ToArray().ShouldBe(sharedValue.Payload.ToArray());
        sharedRead.Value.ContentType.ShouldBe(sharedValue.ContentType);
        sharedRead.Value.SchemaVersion.ShouldBe(sharedValue.SchemaVersion);
        userARead.IsSuccess.ShouldBeTrue();
        userARead.Value.ShouldNotBeNull();
        Encoding.UTF8.GetString(userARead.Value.Payload.ToArray()).ShouldBe("""{"value":"a"}""");
        userARead.Value.ContentType.ShouldBe("application/json");
        userARead.Value.SchemaVersion.ShouldBe(4);
        userBRead.IsSuccess.ShouldBeTrue();
        userBRead.Value.ShouldNotBeNull();
        Encoding.UTF8.GetString(userBRead.Value.Payload.ToArray()).ShouldBe("""{"value":"b"}""");
        reloadedUserARead.IsSuccess.ShouldBeTrue();
        reloadedUserARead.Value.ShouldNotBeNull();
        Encoding.UTF8.GetString(reloadedUserARead.Value.Payload.ToArray()).ShouldBe("""{"value":"a"}""");
        otherPluginRead.IsSuccess.ShouldBeTrue();
        otherPluginRead.Value.ShouldBeNull();
        userAKeys.IsSuccess.ShouldBeTrue();
        userAKeys.Value.ShouldBe([userKey]);
        sharedExists.IsSuccess.ShouldBeTrue();
        sharedExists.Value.ShouldBeTrue();
        deleted.IsSuccess.ShouldBeTrue();
        deleted.Value.ShouldBeTrue();
        deletedAgain.IsSuccess.ShouldBeTrue();
        deletedAgain.Value.ShouldBeFalse();
        invalidSet.IsSuccess.ShouldBeFalse();
        invalidSet.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        invalidGet.IsSuccess.ShouldBeFalse();
        invalidGet.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        invalidList.IsSuccess.ShouldBeFalse();
        invalidList.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }

    [Fact]
    public async Task Persistence_failures_return_typed_database_unavailable_results()
    {
        var storage = new HostPluginStorage(
            "plugin.sample",
            new ThrowingPersistenceExecutor(),
            new ThrowingTransactionScope(),
            () => "user-a",
            FullAccessMutationGate());
        var key = PluginStorageKey.Create("settings/theme").Value;
        var prefix = PluginStorageKeyPrefix.Create("settings").Value;
        var value = PluginStorageValue.FromUtf8Json("""{"theme":"blue"}""");

        var get = await storage.SharedDomainStorage.GetAsync(key);
        var set = await storage.SharedDomainStorage.SetAsync(key, value);
        var exists = await storage.SharedDomainStorage.ExistsAsync(key);
        var list = await storage.SharedDomainStorage.ListKeysAsync(prefix);
        var delete = await storage.SharedDomainStorage.DeleteAsync(key);

        get.IsSuccess.ShouldBeFalse();
        get.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        set.IsSuccess.ShouldBeFalse();
        set.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        exists.IsSuccess.ShouldBeFalse();
        exists.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        list.IsSuccess.ShouldBeFalse();
        list.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        delete.IsSuccess.ShouldBeFalse();
        delete.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
    }

    [Fact]
    public async Task Denied_runtime_mode_blocks_storage_mutation_and_preserves_existing_value()
    {
        using var fixture = HostPluginStorageFixture.Create();
        var key = PluginStorageKey.Create("settings/theme").Value;
        var original = PluginStorageValue.FromUtf8Json("""{"theme":"blue"}""");
        var replacement = PluginStorageValue.FromUtf8Json("""{"theme":"red"}""");
        var writable = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            FullAccessMutationGate());
        var denied = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            new HostRuntimeModeMutationGate(_ =>
                Result.Success(Dobby.Contracts.Licensing.PluginRuntimeMode.Denied)));

        (await writable.SharedDomainStorage.SetAsync(key, original)).IsSuccess.ShouldBeTrue();

        var set = await denied.SharedDomainStorage.SetAsync(key, replacement);
        var delete = await denied.SharedDomainStorage.DeleteAsync(key);
        var persisted = await denied.SharedDomainStorage.GetAsync(key);

        set.ErrorCode.ShouldBe(ErrorCode.LicenseDenied);
        delete.ErrorCode.ShouldBe(ErrorCode.LicenseDenied);
        persisted.IsSuccess.ShouldBeTrue();
        persisted.Value.ShouldNotBeNull();
        persisted.Value.Payload.ToArray().ShouldBe(original.Payload.ToArray());
    }

    [Fact]
    public async Task Delete_data_retention_removes_all_sqlite_compartments_for_only_the_target_plugin()
    {
        using var fixture = HostPluginStorageFixture.Create();
        var target = new HostPluginStorage(
            "plugin.sample",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            FullAccessMutationGate());
        var other = new HostPluginStorage(
            "plugin.sample.extra",
            fixture.Executor,
            fixture.TransactionScope,
            () => "user-a",
            FullAccessMutationGate());
        var sharedKey = PluginStorageKey.Create("settings/theme").Value;
        var userKey = PluginStorageKey.Create("drafts/current").Value;
        var value = PluginStorageValue.FromUtf8Json("""{"value":"preserved"}""");
        (await target.SharedDomainStorage.SetAsync(sharedKey, value)).IsSuccess.ShouldBeTrue();
        (await target.UserScopedStorage.SetAsync(userKey, value)).IsSuccess.ShouldBeTrue();
        (await other.SharedDomainStorage.SetAsync(sharedKey, value)).IsSuccess.ShouldBeTrue();
        var retention = new PluginDataRetentionStore(fixture.Root, fixture.TransactionScope);

        var result = retention.DeletePluginData("plugin.sample");

        result.IsSuccess.ShouldBeTrue();
        (await target.SharedDomainStorage.GetAsync(sharedKey)).Value.ShouldBeNull();
        (await target.UserScopedStorage.GetAsync(userKey)).Value.ShouldBeNull();
        (await other.SharedDomainStorage.GetAsync(sharedKey)).Value.ShouldNotBeNull();
    }

    [Fact]
    public void Delete_data_retention_database_failure_preserves_legacy_plugin_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "dobby-plugin-retention-" + Guid.NewGuid().ToString("N"));
        var targetShared = Path.Combine(root, "plugins", "storage", "shared-domain", "plugin.sample");
        var targetUser = Path.Combine(root, "plugins", "storage", "user-scoped", "plugin.sample");
        var prefixCollision = Path.Combine(root, "plugins", "storage", "shared-domain", "plugin.sample.extra");
        try
        {
            Directory.CreateDirectory(targetShared);
            Directory.CreateDirectory(targetUser);
            Directory.CreateDirectory(prefixCollision);
            var retention = new PluginDataRetentionStore(root, new ThrowingTransactionScope());

            var result = retention.DeletePluginData("plugin.sample");

            result.IsSuccess.ShouldBeFalse();
            result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
            Directory.Exists(targetShared).ShouldBeTrue();
            Directory.Exists(targetUser).ShouldBeTrue();
            Directory.Exists(prefixCollision).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HostRuntimeModeMutationGate FullAccessMutationGate()
    {
        return new HostRuntimeModeMutationGate(_ =>
            Result.Success(Dobby.Contracts.Licensing.PluginRuntimeMode.FullAccess));
    }

    private sealed class HostPluginStorageFixture : IDisposable
    {
        private HostPluginStorageFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            var materialSource = new StaticProtectedMaterialSource(new ProtectedMaterialSet(
                Enumerable.Repeat((byte)0x11, 32).ToArray(),
                Enumerable.Repeat((byte)0x22, 32).ToArray(),
                Enumerable.Repeat((byte)0x33, 32).ToArray(),
                Enumerable.Repeat((byte)0x44, 32).ToArray()));
            var factory = new EncryptedSqliteConnectionFactory(options, materialSource);
            var migrator = new HostPersistenceMigrator(factory, materialSource);
            migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
            migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
            Executor = new HostPersistenceCommandExecutor(factory);
            TransactionScope = new HostPersistenceTransactionScope(factory);
        }

        public string Root { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public HostPersistenceTransactionScope TransactionScope { get; }

        public static HostPluginStorageFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-plugin-storage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new HostPluginStorageFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class StaticProtectedMaterialSource(ProtectedMaterialSet material) : IProtectedMaterialSource
    {
        public ProtectedMaterialSet LoadRequired() => material;
    }

    private sealed class ThrowingPersistenceExecutor : IHostPersistenceCommandExecutor
    {
        public int Execute(string sql, object? parameters = null)
        {
            throw new SimulatedDbException();
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            throw new SimulatedDbException();
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            throw new SimulatedDbException();
        }
    }

    private sealed class ThrowingTransactionScope : IHostPersistenceTransactionScope
    {
        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            action(new ThrowingPersistenceExecutor());
        }
    }

    private sealed class SimulatedDbException : DbException
    {
        public SimulatedDbException()
            : base("simulated storage failure")
        {
        }
    }

    private sealed class StorageAwareModule : IPluginModule
    {
        public void RegisterServices(IPluginServiceCollection services)
        {
            services.AddExtensionPoint<IPluginPage, StorageAwarePage>();
            services.AddInternal<StorageAwarePage, StorageAwarePage>(PluginLifetime.Singleton);
        }

        public void OnActivated(IPluginActivationContext context)
        {
        }
    }

    private sealed class StorageAwarePage(IPluginStorage storage) : IPluginPage
    {
        public IPluginStorage Storage { get; } = storage;

        public PluginPageDescriptor Descriptor { get; } = new("storage.page", "storage.page", "storage.page.label");

        public ValueTask<Result<IPluginPageModel>> LoadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Failure<IPluginPageModel>(ErrorCode.NotFound));
        }

        public ValueTask<Result> ActivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> DeactivateAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }

        public ValueTask<Result> UnloadAsync(PluginPageContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(Result.Success());
        }
    }

    private sealed class NoopEventBus : Dobby.Contracts.Events.IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : Dobby.Contracts.Events.PublicIntegrationEvent
        {
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : Dobby.Contracts.Events.PublicIntegrationEvent
        {
            return new NoopSubscription();
        }

        private sealed class NoopSubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class NoopBackgroundOperationReporter : Dobby.Contracts.BackgroundOperations.IBackgroundOperationReporter
    {
        public IProgress<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot> Progress { get; } =
            new Progress<Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot>();
    }

    private sealed class NoopBackgroundOperationCancellation : Dobby.Contracts.BackgroundOperations.IBackgroundOperationCancellation
    {
        public CancellationToken CancellationToken => CancellationToken.None;
    }
}
