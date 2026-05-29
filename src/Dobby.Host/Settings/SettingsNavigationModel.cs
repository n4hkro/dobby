using Dobby.Configuration;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Localization;
using Dobby.Security.Authorization;

namespace Dobby.Host.Settings;

public sealed class SettingsNavigationModel(
    ICapabilityPolicy<OperationPermissionCapability> authorizationPolicy,
    ISettingsPreferenceSource preferenceSource)
{
    public IReadOnlyList<SettingsCategory> Build(string userId, CorrelationId correlationId)
    {
        _ = preferenceSource.Load();
        var userManagement = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(new OperationKey(
                BuiltInOperationTargets.UserManagementTargetId,
                OperationVerb.View).ToString()),
            new PolicyContext(userId, null, correlationId));

        return
        [
            new SettingsCategory("appearance", HostResourceKeys.SettingsAppearance, true),
            new SettingsCategory("account", HostResourceKeys.SettingsAccount, true),
            new SettingsCategory("security-session", HostResourceKeys.SettingsSecuritySession, true),
            new SettingsCategory("user-management", HostResourceKeys.SettingsUserManagement, userManagement.IsSuccess && userManagement.Value.Allowed),
            new SettingsCategory("about", HostResourceKeys.SettingsAbout, true)
        ];
    }
}
