using System.Reflection;
using Dobby.Contracts.PluginFramework;
using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public sealed class PluginServiceRegistrationBridge : IAsyncDisposable
{
    private readonly object lifecycleGate = new();
    private readonly PluginExtensionPointRegistry extensionPoints;
    private readonly PluginLifetimeManager lifetimeManager;
    private readonly Dictionary<string, long> preparingTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PluginServiceScope> preparedScopes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PluginServiceScope> publishedScopes = new(StringComparer.Ordinal);
    private long nextStateToken;
    private bool disposing;

    public PluginServiceRegistrationBridge(
        PluginExtensionPointRegistry extensionPoints,
        PluginLifetimeManager lifetimeManager,
        PluginHostServices? hostServices = null)
    {
        this.extensionPoints = extensionPoints ?? throw new ArgumentNullException(nameof(extensionPoints));
        this.lifetimeManager = lifetimeManager ?? throw new ArgumentNullException(nameof(lifetimeManager));
        HostServices = hostServices ?? PluginHostServices.CreateDefault();
    }

    public PluginHostServices HostServices { get; }

    public Result<PluginServiceScope> PrepareRegistration(string pluginId, IPluginModule module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(module);

        long stateToken;
        lock (lifecycleGate)
        {
            if (disposing ||
                preparingTokens.ContainsKey(pluginId) ||
                preparedScopes.ContainsKey(pluginId) ||
                publishedScopes.ContainsKey(pluginId))
            {
                return Result.Failure<PluginServiceScope>(ErrorCode.AlreadyExists);
            }

            stateToken = ++nextStateToken;
            preparingTokens[pluginId] = stateToken;
        }

        PluginServiceScope scope;
        try
        {
            var builder = new PluginServiceCollectionBuilder();
            module.RegisterServices(builder);
            scope = new PluginServiceScope(pluginId, stateToken, builder.Descriptors, HostServices, lifetimeManager);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            ClearPreparingToken(pluginId, stateToken);
            return Result.Failure<PluginServiceScope>(ErrorCode.PluginInternalError);
        }

        lock (lifecycleGate)
        {
            if (disposing ||
                !preparingTokens.TryGetValue(pluginId, out var currentToken) ||
                currentToken != stateToken)
            {
                return Result.Failure<PluginServiceScope>(ErrorCode.Cancelled);
            }

            preparingTokens.Remove(pluginId);
            preparedScopes[pluginId] = scope;
        }

        return Result.Success(scope);
    }

    public ValueTask<Result> CommitPreparedAsync(
        PluginServiceScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Result.Failure(ErrorCode.Cancelled));
        }

        var commit = CommitPrepared(scope, cancellationToken, out var scopeToDispose);
        return scopeToDispose is null
            ? ValueTask.FromResult(commit)
            : DisposeAfterCommitFailureAsync(commit, scopeToDispose);
    }

    public async ValueTask CleanupPluginAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var scopes = new List<PluginServiceScope>();
        lock (lifecycleGate)
        {
            preparingTokens.Remove(pluginId);
            if (preparedScopes.Remove(pluginId, out var prepared))
            {
                scopes.Add(prepared);
            }

            if (publishedScopes.Remove(pluginId, out var published) &&
                published is not null &&
                !ContainsReference(scopes, published))
            {
                scopes.Add(published);
            }

            extensionPoints.RemovePlugin(pluginId);
        }

        foreach (var scope in scopes)
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask CleanupAllAsync()
    {
        List<PluginServiceScope> scopes;
        lock (lifecycleGate)
        {
            disposing = true;
            preparingTokens.Clear();
            scopes = [];
            foreach (var scope in preparedScopes.Values.Concat(publishedScopes.Values))
            {
                if (!ContainsReference(scopes, scope))
                {
                    scopes.Add(scope);
                }
            }
            preparedScopes.Clear();
            publishedScopes.Clear();

            foreach (var pluginId in scopes.Select(scope => scope.PluginId).Distinct(StringComparer.Ordinal))
            {
                extensionPoints.RemovePlugin(pluginId);
            }
        }

        foreach (var scope in scopes)
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAllAsync().ConfigureAwait(false);
    }

    public Result<PluginServiceScope> Register(string pluginId, IPluginModule module)
    {
        var prepared = PrepareRegistration(pluginId, module);
        if (!prepared.IsSuccess)
        {
            return prepared;
        }

        var commit = CommitPrepared(prepared.Value, CancellationToken.None, out _);
        return commit.IsSuccess
            ? prepared
            : Result.Failure<PluginServiceScope>(commit.ErrorCode ?? ErrorCode.PluginInternalError);
    }

    private Result CommitPrepared(
        PluginServiceScope scope,
        CancellationToken cancellationToken,
        out PluginServiceScope? scopeToDispose)
    {
        scopeToDispose = null;

        if (cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(ErrorCode.Cancelled);
        }

        PluginPreparedExtensionPoint[] extensionRegistrations;
        try
        {
            extensionRegistrations = scope.GetExtensionPointRegistrations();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            RemovePreparedScope(scope);
            scopeToDispose = scope;
            return Result.Failure(ErrorCode.PluginInternalError);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            RemovePreparedScope(scope);
            scopeToDispose = scope;
            return Result.Failure(ErrorCode.Cancelled);
        }

        lock (lifecycleGate)
        {
            if (disposing ||
                !preparedScopes.TryGetValue(scope.PluginId, out var currentScope) ||
                !ReferenceEquals(currentScope, scope) ||
                currentScope.StateToken != scope.StateToken)
            {
                scopeToDispose = scope;
                return Result.Failure(ErrorCode.Cancelled);
            }

            preparedScopes.Remove(scope.PluginId);
            publishedScopes[scope.PluginId] = scope;
            extensionPoints.Publish(scope.PluginId, extensionRegistrations);
        }

        return Result.Success();
    }

    private void RemovePreparedScope(PluginServiceScope scope)
    {
        lock (lifecycleGate)
        {
            if (preparedScopes.TryGetValue(scope.PluginId, out var currentScope) &&
                ReferenceEquals(currentScope, scope))
            {
                preparedScopes.Remove(scope.PluginId);
            }
        }
    }

    private void ClearPreparingToken(string pluginId, long stateToken)
    {
        lock (lifecycleGate)
        {
            if (preparingTokens.TryGetValue(pluginId, out var currentToken) &&
                currentToken == stateToken)
            {
                preparingTokens.Remove(pluginId);
            }
        }
    }

    private static async ValueTask<Result> DisposeAfterCommitFailureAsync(
        Result result,
        PluginServiceScope scope)
    {
        await scope.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    private static bool ContainsReference<T>(IEnumerable<T> items, T candidate)
        where T : class
    {
        return items.Any(item => ReferenceEquals(item, candidate));
    }
}

public sealed class PluginServiceScope : IAsyncDisposable
{
    private readonly IReadOnlyList<PluginServiceDescriptor> descriptors;
    private readonly Dictionary<PluginServiceDescriptor, object> scopedInstances = [];
    private readonly PluginHostServices hostServices;
    private readonly PluginLifetimeManager lifetimeManager;
    private PluginPreparedExtensionPoint[]? extensionPointRegistrations;
    private int disposed;

    internal PluginServiceScope(
        string pluginId,
        long stateToken,
        IReadOnlyList<PluginServiceDescriptor> descriptors,
        PluginHostServices hostServices,
        PluginLifetimeManager lifetimeManager)
    {
        PluginId = pluginId;
        StateToken = stateToken;
        this.descriptors = descriptors;
        this.hostServices = hostServices;
        this.lifetimeManager = lifetimeManager;
    }

    public string PluginId { get; }

    internal long StateToken { get; }

    public TService Resolve<TService>()
        where TService : class
    {
        return (TService)Resolve(typeof(TService));
    }

    internal PluginPreparedExtensionPoint[] GetExtensionPointRegistrations()
    {
        extensionPointRegistrations ??= descriptors
            .Where(descriptor => descriptor.IsExtensionPoint)
            .Select(descriptor => new PluginPreparedExtensionPoint(
                descriptor.ServiceType,
                ResolveDescriptor(descriptor)))
            .ToArray();

        return extensionPointRegistrations;
    }

    internal object ResolveDescriptor(PluginServiceDescriptor descriptor)
    {
        if (descriptor.Lifetime is PluginLifetime.Singleton or PluginLifetime.Scoped &&
            scopedInstances.TryGetValue(descriptor, out var existing))
        {
            return existing;
        }

        var instance = Create(descriptor.ImplementationType);
        TrackDisposable(instance);
        if (descriptor.Lifetime is PluginLifetime.Singleton or PluginLifetime.Scoped)
        {
            scopedInstances[descriptor] = instance;
        }

        return instance;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return ValueTask.CompletedTask;
        }

        return lifetimeManager.DisposePluginAsync(PluginId);
    }

    private object Resolve(Type serviceType)
    {
        var descriptor = descriptors.SingleOrDefault(descriptor =>
            !descriptor.IsExtensionPoint &&
            descriptor.ServiceType == serviceType);
        if (descriptor is not null)
        {
            return ResolveDescriptor(descriptor);
        }

        if (hostServices.TryResolve(serviceType, out var hostService))
        {
            return hostService;
        }

        throw new InvalidOperationException($"Plugin-private service '{serviceType.FullName}' is not registered.");
    }

    private object Create(Type implementationType)
    {
        var constructors = implementationType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .ToArray();

        foreach (var constructor in constructors)
        {
            try
            {
                var arguments = constructor.GetParameters()
                    .Select(parameter => Resolve(parameter.ParameterType))
                    .ToArray();
                return constructor.Invoke(arguments);
            }
            catch (InvalidOperationException)
            {
            }
        }

        throw new InvalidOperationException($"No resolvable public constructor exists for '{implementationType.FullName}'.");
    }

    private void TrackDisposable(object instance)
    {
        if (instance is not IDisposable and not IAsyncDisposable)
        {
            return;
        }

        lifetimeManager.TrackSingleton(PluginId, instance);
    }
}

internal sealed record PluginPreparedExtensionPoint(
    Type ContractType,
    object Instance);
