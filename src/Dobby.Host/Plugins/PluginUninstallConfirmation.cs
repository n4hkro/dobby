using System.Windows;

namespace Dobby.Host.Plugins;

public sealed record PluginUninstallConfirmationRequest(
    string PluginId,
    string DisplayName,
    bool WasActivatedInCurrentProcess);

public sealed record PluginUninstallConfirmationResult(
    bool Confirmed,
    PluginUninstallDataChoice DataChoice)
{
    public static PluginUninstallConfirmationResult Cancelled { get; } =
        new(false, PluginUninstallDataChoice.KeepData);

    public static PluginUninstallConfirmationResult Confirm(PluginUninstallDataChoice dataChoice)
    {
        return new PluginUninstallConfirmationResult(true, dataChoice);
    }
}

public interface IPluginUninstallConfirmation
{
    PluginUninstallConfirmationResult Confirm(PluginUninstallConfirmationRequest request);
}

public sealed class WpfPluginUninstallConfirmation(Window owner) : IPluginUninstallConfirmation
{
    public PluginUninstallConfirmationResult Confirm(PluginUninstallConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = MessageBox.Show(
            owner,
            $"Uninstall {request.DisplayName}? Choose Yes to delete plugin data, No to keep plugin data, or Cancel to abort.",
            "Uninstall plugin",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);

        return response switch
        {
            MessageBoxResult.Yes => PluginUninstallConfirmationResult.Confirm(PluginUninstallDataChoice.DeleteData),
            MessageBoxResult.No => PluginUninstallConfirmationResult.Confirm(PluginUninstallDataChoice.KeepData),
            _ => PluginUninstallConfirmationResult.Cancelled
        };
    }
}
