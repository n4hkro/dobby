using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.SDK.PluginFramework;

namespace Dobby.Sample.Plugin;

public sealed class SamplePluginModule : PluginBase
{
    public override void RegisterServices(IPluginServiceCollection services)
    {
        services.AddExtensionPoint<IPluginPage, SampleHomePage>();
    }
}

public sealed class SampleHomePage : IPluginPage
{
    public PluginPageDescriptor Descriptor { get; } = new(
        "sample.home",
        "sample.home",
        "sample.home.label");

    public ValueTask<Result<IPluginPageModel>> LoadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success<IPluginPageModel>(new SampleHomePageModel("sample.home.ready")));
    }

    public ValueTask<Result> ActivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result> DeactivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result> UnloadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed record SampleHomePageModel(string MessageKey) : IPluginPageModel;
