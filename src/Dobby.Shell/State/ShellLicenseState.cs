using Dobby.Contracts.Licensing;
using Dobby.Localization;

namespace Dobby.Shell.State;

internal sealed class ShellLicenseState
{
    public bool CanViewStatus { get; private set; }

    public bool CanImport { get; private set; }

    public bool ImportBusy { get; private set; }

    public PluginRuntimeMode RuntimeMode { get; private set; } = PluginRuntimeMode.Denied;

    public string? FeedbackKey { get; private set; }

    public string? PluginId { get; private set; }

    public string? PluginName { get; private set; }

    public void SetSurface(
        bool canViewStatus,
        bool canImport,
        PluginRuntimeMode runtimeMode,
        string? feedbackKey,
        string? pluginId,
        string? pluginName)
    {
        CanViewStatus = canViewStatus;
        CanImport = canImport;
        RuntimeMode = runtimeMode;
        FeedbackKey = feedbackKey;
        PluginId = pluginId;
        PluginName = pluginName;
    }

    public void SetFeedback(string? feedbackKey) => FeedbackKey = feedbackKey;

    public void SetImportBusy(bool isBusy)
    {
        ImportBusy = isBusy;
        if (isBusy)
        {
            FeedbackKey = HostResourceKeys.LicenseImportBusy;
        }
        else if (FeedbackKey == HostResourceKeys.LicenseImportBusy)
        {
            FeedbackKey = null;
        }
    }

    public void SetRuntimeMode(PluginRuntimeMode runtimeMode, string? feedbackKey)
    {
        RuntimeMode = runtimeMode;
        FeedbackKey = feedbackKey;
    }

    public bool IsActionAllowed(PluginRuntimeAction action)
    {
        return PluginRuntimeActionPolicy.Evaluate(RuntimeMode, action) ==
            PluginRuntimeActionDecision.Allowed;
    }

    public string ListDisplayText(HostStringCatalog strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        if (!string.IsNullOrWhiteSpace(PluginName) && !string.IsNullOrWhiteSpace(PluginId))
        {
            return $"{PluginName} ({PluginId})";
        }

        if (!string.IsNullOrWhiteSpace(PluginName))
        {
            return PluginName;
        }

        return string.IsNullOrWhiteSpace(PluginId)
            ? strings.GetString(HostResourceKeys.LicenseNoPlugin)
            : PluginId;
    }
}
