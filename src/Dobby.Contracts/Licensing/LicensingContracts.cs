using Dobby.Core;

namespace Dobby.Contracts.Licensing;

public enum PluginRuntimeMode
{
    FullAccess,
    ReadOnlyExpired,
    Denied
}

public enum PluginRuntimeAction
{
    View,
    PluginAction,
    StorageRead,
    StorageWrite,
    BackgroundStart,
    InitialPackageInstall,
    PackageUpdate,
    EnableDisableUninstall,
    DomainExport,
    PhysicalPrint,
    VirtualOrFileOutput,
    HostDiagnostics,
    PluginDiagnostics
}

public enum PluginRuntimeActionDecision
{
    Allowed,
    RequiresPhysicalPrintConfirmation,
    Denied
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PluginRuntimeActionOwnerAttribute(params PluginRuntimeAction[] actions) : Attribute
{
    public IReadOnlyList<PluginRuntimeAction> Actions { get; } = actions is { Length: > 0 }
        ? Array.AsReadOnly(actions.Distinct().ToArray())
        : throw new ArgumentException("At least one licensed runtime action is required.", nameof(actions));
}

public static class PluginRuntimeActionPolicy
{
    public static PluginRuntimeActionDecision Evaluate(
        PluginRuntimeMode mode,
        PluginRuntimeAction action)
    {
        if (!Enum.IsDefined(mode) || !Enum.IsDefined(action))
        {
            return PluginRuntimeActionDecision.Denied;
        }

        if (action == PluginRuntimeAction.HostDiagnostics)
        {
            return PluginRuntimeActionDecision.Allowed;
        }

        if (action == PluginRuntimeAction.InitialPackageInstall)
        {
            return mode == PluginRuntimeMode.ReadOnlyExpired
                ? PluginRuntimeActionDecision.Denied
                : PluginRuntimeActionDecision.Allowed;
        }

        return mode switch
        {
            PluginRuntimeMode.FullAccess => PluginRuntimeActionDecision.Allowed,
            PluginRuntimeMode.ReadOnlyExpired when action is PluginRuntimeAction.View or PluginRuntimeAction.StorageRead =>
                PluginRuntimeActionDecision.Allowed,
            PluginRuntimeMode.ReadOnlyExpired when action == PluginRuntimeAction.PhysicalPrint =>
                PluginRuntimeActionDecision.RequiresPhysicalPrintConfirmation,
            _ => PluginRuntimeActionDecision.Denied
        };
    }
}

public interface ICapabilityLicensing
{
    Result<PluginRuntimeMode> GetCurrentMode();
}
