using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Navigation;
using Dobby.PluginFramework.Activation;
using Dobby.Shell;

namespace Dobby.Host.Plugins;

public sealed class PluginPageRouteCoordinator(
    PluginExtensionPointRegistry extensionPointRegistry,
    ShellViewModel shellViewModel,
    Func<string?>? currentUserIdProvider = null,
    Action<string, string, string, ErrorCode>? pageFaultSink = null)
{
    private ActivePluginPage? activePage;

    public async ValueTask<Result> ActivateRouteAsync(
        string? routeKey,
        CancellationToken cancellationToken)
    {
        var activeRouteKey = shellViewModel.ResolveRouteOrHome(routeKey);
        if (IsBuiltInRoute(activeRouteKey))
        {
            var clear = await ClearActivePageAsync(cancellationToken).ConfigureAwait(false);
            if (!clear.IsSuccess)
            {
                return clear;
            }

            shellViewModel.ActivateResolvedRoute(activeRouteKey);
            shellViewModel.SetActivePluginPage(null);
            return Result.Success();
        }

        var lookup = await FindPluginPageRouteRegistrationAsync(
            activeRouteKey,
            cancellationToken).ConfigureAwait(false);
        if (!lookup.IsSuccess)
        {
            return Result.Failure(lookup.ErrorCode ?? ErrorCode.PluginInternalError);
        }

        var registration = lookup.Value.Registration;
        if (registration is null)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var page = registration.Page;
        var descriptor = registration.Descriptor;
        if (activePage is not null &&
            activePage.RouteKey.Equals(registration.RouteKey, StringComparison.Ordinal))
        {
            if (activePage.Phase == ActivePluginPagePhase.Deactivated)
            {
                var reactivate = await InvokePageLifecycleAsync(
                    () => activePage.Page.ActivateAsync(activePage.Context, cancellationToken),
                    activePage.Context,
                    "activate",
                    cancellationToken).ConfigureAwait(false);
                if (!reactivate.IsSuccess)
                {
                    return reactivate;
                }

                activePage = activePage with { Phase = ActivePluginPagePhase.Active };
                shellViewModel.ActivateResolvedRoute(activeRouteKey);
                shellViewModel.SetActivePluginPage(activePage.Model);
                return Result.Success();
            }

            shellViewModel.ActivateResolvedRoute(activeRouteKey);
            return Result.Success();
        }

        var context = new PluginPageContext(
            registration.PluginId,
            descriptor.ExtensionPointId,
            registration.RouteKey,
            CorrelationId.New().ToString(),
            currentUserIdProvider?.Invoke());

        var load = await InvokePageLoadAsync(
            () => page.LoadAsync(context, cancellationToken),
            context,
            cancellationToken).ConfigureAwait(false);
        if (!load.IsSuccess)
        {
            return Result.Failure(load.ErrorCode ?? ErrorCode.PluginInternalError);
        }

        var activate = await InvokePageLifecycleAsync(
            () => page.ActivateAsync(context, cancellationToken),
            context,
            "activate",
            cancellationToken).ConfigureAwait(false);
        if (!activate.IsSuccess)
        {
            await CleanupLoadedPageAsync(page, context, cancellationToken).ConfigureAwait(false);
            return activate;
        }

        if (activePage is not null &&
            !activePage.RouteKey.Equals(registration.RouteKey, StringComparison.Ordinal))
        {
            var clear = await ClearActivePageAsync(cancellationToken).ConfigureAwait(false);
            if (!clear.IsSuccess)
            {
                await CleanupUncommittedPageAsync(page, context, cancellationToken).ConfigureAwait(false);
                return clear;
            }
        }

        shellViewModel.ActivateResolvedRoute(activeRouteKey);
        activePage = new ActivePluginPage(
            registration.PluginId,
            registration.RouteKey,
            page,
            context,
            load.Value,
            ActivePluginPagePhase.Active);
        shellViewModel.SetActivePluginPage(load.Value);
        return Result.Success();
    }

    private async ValueTask<Result<PluginPageRouteLookup>> FindPluginPageRouteRegistrationAsync(
        string activeRouteKey,
        CancellationToken cancellationToken)
    {
        foreach (var item in extensionPointRegistry.GetRegistrationsSnapshot<IPluginPage>())
        {
            var registration = await BuildRouteRegistrationAsync(item, cancellationToken)
                .ConfigureAwait(false);
            if (!registration.IsSuccess)
            {
                if (registration.ErrorCode == ErrorCode.Cancelled &&
                    cancellationToken.IsCancellationRequested)
                {
                    return Result.Failure<PluginPageRouteLookup>(ErrorCode.Cancelled);
                }

                continue;
            }

            if (registration.Value.RouteKey.Equals(activeRouteKey, StringComparison.Ordinal))
            {
                return Result.Success(new PluginPageRouteLookup(registration.Value));
            }
        }

        return Result.Success(new PluginPageRouteLookup(null));
    }

    private async ValueTask<Result<PluginPageRouteRegistration>> BuildRouteRegistrationAsync(
        PluginExtensionPointRegistration<IPluginPage> item,
        CancellationToken cancellationToken)
    {
        var bounded = await PluginExceptionBoundary.InvokeAsync(
            () =>
            {
                var descriptor = item.Instance.Descriptor;
                var routeKey = PluginRouteNormalizer.Normalize(item.PluginId, descriptor.RouteKey);
                return Task.FromResult(new PluginPageRouteRegistration(
                    item.PluginId,
                    item.Instance,
                    descriptor,
                    routeKey));
            },
            cancellationToken).ConfigureAwait(false);

        if (bounded.IsSuccess)
        {
            return Result.Success(bounded.Value);
        }

        var errorCode = bounded.ErrorCode ?? ErrorCode.PluginInternalError;
        RecordPageFault(
            item.PluginId,
            $"plugin/{item.PluginId}/unknown",
            "descriptor",
            errorCode,
            cancellationToken);
        return Result.Failure<PluginPageRouteRegistration>(errorCode);
    }

    private async ValueTask<Result> ClearActivePageAsync(CancellationToken cancellationToken)
    {
        if (activePage is null)
        {
            return Result.Success();
        }

        if (activePage.Phase == ActivePluginPagePhase.Active)
        {
            var deactivate = await InvokePageLifecycleAsync(
                () => activePage.Page.DeactivateAsync(activePage.Context, cancellationToken),
                activePage.Context,
                "deactivate",
                cancellationToken).ConfigureAwait(false);
            if (!deactivate.IsSuccess)
            {
                return deactivate;
            }

            activePage = activePage with { Phase = ActivePluginPagePhase.Deactivated };
            shellViewModel.SetActivePluginPage(null);
        }

        var unload = await InvokePageLifecycleAsync(
            () => activePage.Page.UnloadAsync(activePage.Context, cancellationToken),
            activePage.Context,
            "unload",
            cancellationToken).ConfigureAwait(false);
        if (!unload.IsSuccess)
        {
            return unload;
        }

        activePage = null;
        return Result.Success();
    }

    private async ValueTask CleanupLoadedPageAsync(
        IPluginPage page,
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        _ = await InvokePageLifecycleAsync(
            () => page.UnloadAsync(context, cancellationToken),
            context,
            "unload",
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask CleanupUncommittedPageAsync(
        IPluginPage page,
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        _ = await InvokePageLifecycleAsync(
            () => page.DeactivateAsync(context, cancellationToken),
            context,
            "deactivate",
            cancellationToken).ConfigureAwait(false);
        _ = await InvokePageLifecycleAsync(
            () => page.UnloadAsync(context, cancellationToken),
            context,
            "unload",
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result> InvokePageLifecycleAsync(
        Func<ValueTask<Result>> pluginCall,
        PluginPageContext context,
        string operation,
        CancellationToken cancellationToken)
    {
        var bounded = await PluginExceptionBoundary.InvokeAsync(
            async () => await pluginCall().ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        var result = bounded.IsSuccess
            ? bounded.Value
            : Result.Failure(bounded.ErrorCode ?? ErrorCode.PluginInternalError);
        if (!result.IsSuccess)
        {
            RecordPageFault(
                context.PluginId,
                context.RouteKey,
                operation,
                result.ErrorCode ?? ErrorCode.PluginInternalError,
                cancellationToken);
        }

        return result;
    }

    private async ValueTask<Result<IPluginPageModel>> InvokePageLoadAsync(
        Func<ValueTask<Result<IPluginPageModel>>> pluginCall,
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        var bounded = await PluginExceptionBoundary.InvokeAsync(
            async () => await pluginCall().ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        var result = bounded.IsSuccess
            ? bounded.Value
            : Result.Failure<IPluginPageModel>(bounded.ErrorCode ?? ErrorCode.PluginInternalError);
        if (!result.IsSuccess)
        {
            RecordPageFault(
                context.PluginId,
                context.RouteKey,
                "load",
                result.ErrorCode ?? ErrorCode.PluginInternalError,
                cancellationToken);
        }

        return result;
    }

    private void RecordPageFault(
        string pluginId,
        string routeKey,
        string operation,
        ErrorCode errorCode,
        CancellationToken cancellationToken)
    {
        if (errorCode == ErrorCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            return;
        }

        pageFaultSink?.Invoke(pluginId, routeKey, operation, errorCode);
    }

    private static bool IsBuiltInRoute(string routeKey)
    {
        return routeKey is
            RouteRegistry.HomeRouteKey or
            RouteRegistry.PluginsRouteKey or
            RouteRegistry.SettingsRouteKey;
    }

    private sealed record ActivePluginPage(
        string PluginId,
        string RouteKey,
        IPluginPage Page,
        PluginPageContext Context,
        IPluginPageModel Model,
        ActivePluginPagePhase Phase);

    private enum ActivePluginPagePhase
    {
        Active,
        Deactivated
    }

    private sealed record PluginPageRouteRegistration(
        string PluginId,
        IPluginPage Page,
        PluginPageDescriptor Descriptor,
        string RouteKey);

    private sealed record PluginPageRouteLookup(PluginPageRouteRegistration? Registration);
}
