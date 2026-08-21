using Dobby.Infrastructure.Persistence;

namespace Dobby.Host.Plugins;

public sealed class LicenseActivationRefreshOutboxHandler : IHostOutboxMessageHandler
{
    public static HostOutboxRoute Route { get; } = new("license", "activation-refresh-requested");

    private readonly object gate = new();
    private Func<string, ValueTask>? refresh;

    public void Register(Func<string, ValueTask> refreshHandler)
    {
        ArgumentNullException.ThrowIfNull(refreshHandler);
        lock (gate)
        {
            if (refresh is not null)
            {
                throw new InvalidOperationException("The license activation refresh handler is already registered.");
            }

            refresh = refreshHandler;
        }
    }

    public async Task DispatchAsync(HostOutboxDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        cancellationToken.ThrowIfCancellationRequested();
        if (!delivery.Purpose.Equals(Route.Purpose, StringComparison.Ordinal) ||
            !delivery.MessageType.Equals(Route.MessageType, StringComparison.Ordinal) ||
            !delivery.Fields.TryGetValue("pluginId", out var pluginId) ||
            string.IsNullOrWhiteSpace(pluginId) ||
            pluginId.Length > 128 ||
            pluginId.Any(char.IsControl))
        {
            throw new InvalidOperationException("The license activation refresh message is malformed.");
        }

        Func<string, ValueTask>? callback;
        lock (gate)
        {
            callback = refresh;
        }

        if (callback is null)
        {
            throw new InvalidOperationException("The license activation refresh consumer is not ready.");
        }

        await callback(pluginId).ConfigureAwait(false);
    }
}
