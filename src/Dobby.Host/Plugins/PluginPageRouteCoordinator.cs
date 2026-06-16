using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.Shell;

namespace Dobby.Host.Plugins;

public sealed class PluginPageRouteCoordinator(
    PluginExtensionPointRegistry extensionPointRegistry,
    ShellViewModel shellViewModel)
{
    private ActivePluginPage? activePage;

    public async ValueTask<Result> ActivateRouteAsync(
        string? routeKey,
        CancellationToken cancellationToken)
    {
        var route = shellViewModel.ActivateRoute(routeKey);
        if (!route.IsSuccess)
        {
            return route;
        }

        var activeRouteKey = shellViewModel.ActiveRouteKey;
        var registration = extensionPointRegistry
            .GetRegistrationsSnapshot<IPluginPage>()
            .Select(item => new PluginPageRouteRegistration(
                item.PluginId,
                item.Instance,
                PluginRouteNormalizer.Normalize(item.PluginId, item.Instance.Descriptor.RouteKey)))
            .FirstOrDefault(item => item.RouteKey.Equals(activeRouteKey, StringComparison.Ordinal));

        if (registration is null)
        {
            await ClearActivePageAsync(cancellationToken).ConfigureAwait(false);
            shellViewModel.SetActivePluginPage(null);
            return Result.Success();
        }

        var page = registration.Page;
        var descriptor = page.Descriptor;
        var context = new PluginPageContext(
            registration.PluginId,
            descriptor.ExtensionPointId,
            registration.RouteKey,
            CorrelationId.New().ToString());

        if (activePage is not null &&
            !activePage.RouteKey.Equals(registration.RouteKey, StringComparison.Ordinal))
        {
            await ClearActivePageAsync(cancellationToken).ConfigureAwait(false);
        }

        var activate = await page.ActivateAsync(context, cancellationToken).ConfigureAwait(false);
        if (!activate.IsSuccess)
        {
            return activate;
        }

        var load = await page.LoadAsync(context, cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess)
        {
            return Result.Failure(load.ErrorCode ?? ErrorCode.PluginInternalError);
        }

        activePage = new ActivePluginPage(registration.PluginId, registration.RouteKey, page, context);
        shellViewModel.SetActivePluginPage(load.Value);
        return Result.Success();
    }

    private async ValueTask ClearActivePageAsync(CancellationToken cancellationToken)
    {
        if (activePage is null)
        {
            return;
        }

        await activePage.Page.DeactivateAsync(activePage.Context, cancellationToken).ConfigureAwait(false);
        await activePage.Page.UnloadAsync(activePage.Context, cancellationToken).ConfigureAwait(false);
        activePage = null;
    }

    private sealed record ActivePluginPage(
        string PluginId,
        string RouteKey,
        IPluginPage Page,
        PluginPageContext Context);

    private sealed record PluginPageRouteRegistration(
        string PluginId,
        IPluginPage Page,
        string RouteKey);
}
