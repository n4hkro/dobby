using Dobby.Localization;

namespace Dobby.Shell.State;

internal sealed class ShellPluginManagerState(HostStringCatalog strings)
{
    public bool CanView { get; private set; }

    public bool CanImport { get; private set; }

    public bool CanConfigure { get; private set; }

    public bool CanDelete { get; private set; }

    public bool IsBusy { get; private set; }

    public IReadOnlyList<ShellPluginManagerItem> Items { get; private set; } = [];

    public string? SelectedPluginId { get; private set; }

    public string? FeedbackKey { get; private set; }

    public ShellPluginManagerItem? SelectedItem => string.IsNullOrWhiteSpace(SelectedPluginId)
        ? null
        : Items.SingleOrDefault(item => item.PluginId.Equals(SelectedPluginId, StringComparison.Ordinal));

    public void SetSurface(
        bool canView,
        bool canImport,
        bool canConfigure,
        bool canDelete,
        IReadOnlyList<ShellPluginManagerItem> plugins,
        string? feedbackKey,
        bool isBusy)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        CanView = canView;
        CanImport = canImport;
        CanConfigure = canConfigure;
        CanDelete = canDelete;
        IsBusy = isBusy;
        Items = plugins
            .Select(ToPresentation)
            .OrderBy(plugin => plugin.DisplayName, StringComparer.Ordinal)
            .ThenBy(plugin => plugin.PluginId, StringComparer.Ordinal)
            .ToArray();
        FeedbackKey = feedbackKey;
        if (SelectedPluginId is null ||
            Items.All(item => !item.PluginId.Equals(SelectedPluginId, StringComparison.Ordinal)))
        {
            SelectedPluginId = Items.FirstOrDefault()?.PluginId;
        }
    }

    public void Select(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        SelectedPluginId = Items.Any(item => item.PluginId.Equals(pluginId, StringComparison.Ordinal))
            ? pluginId
            : null;
    }

    public void SetBusy(bool isBusy, string? feedbackKey)
    {
        IsBusy = isBusy;
        if (isBusy)
        {
            FeedbackKey = feedbackKey ?? HostResourceKeys.PluginsManagerInstallBusy;
        }
        else if (IsBusyFeedbackKey(FeedbackKey))
        {
            FeedbackKey = null;
        }
    }

    public void SetFeedback(string? feedbackKey) => FeedbackKey = feedbackKey;

    public bool CanUseSelectedAction(bool hasPermission, bool runtimeActionAllowed)
    {
        return hasPermission &&
            CanView &&
            !IsBusy &&
            runtimeActionAllowed &&
            SelectedItem is not null;
    }

    public string DetailText(ShellPluginManagerItem plugin)
    {
        var detail = $"{plugin.DisplayName} {plugin.Version} - {plugin.StatusSummary}";
        return plugin.RestartRequired
            ? $"{detail} - {strings.GetString(HostResourceKeys.PluginsManagerRestartRequired)}"
            : detail;
    }

    public IReadOnlyList<ShellPluginManagerDetailRow> DetailRows(ShellPluginManagerItem plugin)
    {
        var rows = new List<ShellPluginManagerDetailRow>
        {
            new("display-name", strings.GetString(HostResourceKeys.PluginsManagerDetailDisplayName), plugin.DisplayName),
            new("plugin-id", strings.GetString(HostResourceKeys.PluginsManagerDetailPluginId), plugin.PluginId),
            new("version", strings.GetString(HostResourceKeys.PluginsManagerDetailVersion), plugin.Version),
            new("status", strings.GetString(HostResourceKeys.PluginsManagerDetailStatus), plugin.StatusSummary)
        };

        AddIfPresent(rows, "trust", strings.GetString(HostResourceKeys.PluginsManagerDetailTrust), plugin.TrustSummary);
        AddIfPresent(rows, "license", strings.GetString(HostResourceKeys.PluginsManagerDetailLicense), plugin.LicenseSummary);
        AddIfPresent(rows, "fault", strings.GetString(HostResourceKeys.PluginsManagerDetailFault), plugin.FaultSummary);
        AddIfPresent(rows, "rejection", strings.GetString(HostResourceKeys.PluginsManagerDetailRejection), plugin.SafeRejectionText);
        if (plugin.RestartRequired)
        {
            rows.Add(new ShellPluginManagerDetailRow(
                "restart",
                strings.GetString(HostResourceKeys.PluginsManagerRestartRequired),
                strings.GetString(HostResourceKeys.PluginsManagerRestartRequired)));
        }

        return rows;
    }

    private ShellPluginManagerItem ToPresentation(ShellPluginManagerItem plugin)
    {
        var status = plugin.IsPendingUninstall || Contains(plugin.StatusSummary, "pending uninstall")
            ? strings.GetString(HostResourceKeys.PluginsManagerStatusPendingUninstall)
            : Contains(plugin.StatusSummary, "enabled")
                ? strings.GetString(HostResourceKeys.PluginsManagerStatusEnabled)
                : Contains(plugin.StatusSummary, "disabled")
                    ? strings.GetString(HostResourceKeys.PluginsManagerStatusDisabled)
                    : Contains(plugin.StatusSummary, "fault")
                        ? strings.GetString(HostResourceKeys.PluginsManagerStatusFaulted)
                        : IsRejected(plugin.StatusSummary)
                            ? strings.GetString(HostResourceKeys.PluginsManagerStatusRejected)
                            : strings.GetString(HostResourceKeys.PluginsManagerStatusAvailable);
        var trust = plugin.TrustState switch
        {
            ShellPluginTrustState.Verified => strings.GetString(HostResourceKeys.PluginsManagerTrustTrusted),
            ShellPluginTrustState.Rejected => strings.GetString(HostResourceKeys.PluginsManagerTrustRejected),
            _ when string.IsNullOrWhiteSpace(plugin.TrustSummary) => strings.GetString(HostResourceKeys.PluginsManagerTrustUnavailable),
            _ when Contains(plugin.TrustSummary, "trusted") || Contains(plugin.TrustSummary, "verified") => strings.GetString(HostResourceKeys.PluginsManagerTrustTrusted),
            _ => strings.GetString(HostResourceKeys.PluginsManagerTrustRejected)
        };
        var license = string.IsNullOrWhiteSpace(plugin.LicenseSummary)
            ? strings.GetString(HostResourceKeys.PluginsManagerLicenseUnavailable)
            : Contains(plugin.LicenseSummary, "read-only")
                ? strings.GetString(HostResourceKeys.PluginsManagerLicenseReadOnly)
                : Contains(plugin.LicenseSummary, "full")
                    ? strings.GetString(HostResourceKeys.PluginsManagerLicenseFullAccess)
                    : strings.GetString(HostResourceKeys.PluginsManagerLicenseDenied);
        var fault = string.IsNullOrWhiteSpace(plugin.FaultSummary) ||
            Contains(plugin.FaultSummary, "no runtime fault") ||
            Contains(plugin.FaultSummary, "no faults")
            ? strings.GetString(HostResourceKeys.PluginsManagerFaultNone)
            : strings.GetString(HostResourceKeys.PluginsManagerFaultDetected);
        var rejection = IsRejected(plugin.StatusSummary) || !string.IsNullOrWhiteSpace(plugin.SafeRejectionText)
            ? strings.GetString(HostResourceKeys.PluginsManagerRejectionSafe)
            : string.Empty;

        return plugin with
        {
            StatusSummary = status,
            TrustSummary = trust,
            LicenseSummary = license,
            FaultSummary = fault,
            SafeRejectionText = rejection
        };
    }

    private static bool IsBusyFeedbackKey(string? feedbackKey)
    {
        return feedbackKey is HostResourceKeys.PluginsManagerInstallBusy
            or HostResourceKeys.PluginsManagerEnableBusy
            or HostResourceKeys.PluginsManagerDisableBusy
            or HostResourceKeys.PluginsManagerUninstallBusy;
    }

    private static void AddIfPresent(
        ICollection<ShellPluginManagerDetailRow> rows,
        string key,
        string label,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add(new ShellPluginManagerDetailRow(key, label, value));
        }
    }

    private static bool IsRejected(string value) =>
        Contains(value, "rejected") || Contains(value, "invalid");

    private static bool Contains(string value, string expected) =>
        value.Contains(expected, StringComparison.OrdinalIgnoreCase);
}
