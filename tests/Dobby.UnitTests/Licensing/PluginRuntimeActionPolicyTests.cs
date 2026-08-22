using Dobby.Contracts.Licensing;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class PluginRuntimeActionPolicyTests
{
    public static TheoryData<PluginRuntimeMode, PluginRuntimeAction, PluginRuntimeActionDecision> Matrix()
    {
        var data = new TheoryData<PluginRuntimeMode, PluginRuntimeAction, PluginRuntimeActionDecision>();
        foreach (var mode in Enum.GetValues<PluginRuntimeMode>())
        {
            foreach (var action in Enum.GetValues<PluginRuntimeAction>())
            {
                data.Add(mode, action, Expected(mode, action));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Central_action_matrix_is_explicit_for_every_runtime_mode_and_surface(
        PluginRuntimeMode mode,
        PluginRuntimeAction action,
        PluginRuntimeActionDecision expected)
    {
        PluginRuntimeActionPolicy.Evaluate(mode, action).ShouldBe(expected);
    }

    [Theory]
    [InlineData(LicenseLifecycleState.Pending, PluginRuntimeMode.Denied)]
    [InlineData(LicenseLifecycleState.Active, PluginRuntimeMode.FullAccess)]
    [InlineData(LicenseLifecycleState.Grace, PluginRuntimeMode.FullAccess)]
    [InlineData(LicenseLifecycleState.ReadOnlyExpired, PluginRuntimeMode.ReadOnlyExpired)]
    [InlineData(LicenseLifecycleState.Denied, PluginRuntimeMode.Denied)]
    public void Every_v3_lifecycle_state_maps_into_the_action_matrix(
        LicenseLifecycleState lifecycle,
        PluginRuntimeMode expectedMode)
    {
        LicenseRuntimeModeMapper.Map(lifecycle).ShouldBe(expectedMode);
    }

    [Fact]
    public void Unknown_mode_or_action_fails_closed()
    {
        PluginRuntimeActionPolicy.Evaluate(
                (PluginRuntimeMode)int.MaxValue,
                PluginRuntimeAction.View)
            .ShouldBe(PluginRuntimeActionDecision.Denied);
        PluginRuntimeActionPolicy.Evaluate(
                PluginRuntimeMode.FullAccess,
                (PluginRuntimeAction)int.MaxValue)
            .ShouldBe(PluginRuntimeActionDecision.Denied);
    }

    private static PluginRuntimeActionDecision Expected(
        PluginRuntimeMode mode,
        PluginRuntimeAction action)
    {
        if (action is PluginRuntimeAction.HostDiagnostics or PluginRuntimeAction.InitialPackageInstall)
        {
            return action == PluginRuntimeAction.InitialPackageInstall && mode == PluginRuntimeMode.ReadOnlyExpired
                ? PluginRuntimeActionDecision.Denied
                : PluginRuntimeActionDecision.Allowed;
        }

        if (mode == PluginRuntimeMode.FullAccess)
        {
            return PluginRuntimeActionDecision.Allowed;
        }

        if (mode == PluginRuntimeMode.ReadOnlyExpired &&
            action is PluginRuntimeAction.View or PluginRuntimeAction.StorageRead)
        {
            return PluginRuntimeActionDecision.Allowed;
        }

        return mode == PluginRuntimeMode.ReadOnlyExpired && action == PluginRuntimeAction.PhysicalPrint
            ? PluginRuntimeActionDecision.RequiresPhysicalPrintConfirmation
            : PluginRuntimeActionDecision.Denied;
    }
}
