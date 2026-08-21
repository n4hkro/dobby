using Dobby.Contracts.Licensing;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseRuntimeModeServiceTests
{
    [Theory]
    [InlineData(LicenseLifecycleState.NoLicense, PluginRuntimeMode.Denied)]
    [InlineData(LicenseLifecycleState.Pending, PluginRuntimeMode.Denied)]
    [InlineData(LicenseLifecycleState.Active, PluginRuntimeMode.FullAccess)]
    [InlineData(LicenseLifecycleState.Grace, PluginRuntimeMode.FullAccess)]
    [InlineData(LicenseLifecycleState.ReadOnly, PluginRuntimeMode.ReadOnlyExpired)]
    [InlineData(LicenseLifecycleState.Denied, PluginRuntimeMode.Denied)]
    public void Lifecycle_state_maps_to_non_disclosing_plugin_runtime_mode(
        LicenseLifecycleState state,
        PluginRuntimeMode expected)
    {
        LicenseRuntimeModeMapper.Map(state).ShouldBe(expected);
    }

    [Fact]
    public void Evaluate_returns_pending_before_not_before()
    {
        var payload = Payload(notBefore: new DateTimeOffset(2026, 06, 20, 0, 0, 0, TimeSpan.Zero));

        LicenseRuntimeModeService.EvaluateLifecycle(payload, new DateTimeOffset(2026, 06, 19, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(LicenseLifecycleState.Pending);
    }

    [Fact]
    public void Evaluate_grant_is_active_at_not_before_boundary()
    {
        var payload = Payload();

        LicenseRuntimeModeService.EvaluateLifecycle(payload, new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(LicenseLifecycleState.Active);
    }

    [Fact]
    public void Evaluate_revocation_and_unhealthy_trusted_time_are_denied()
    {
        var now = new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero);

        LicenseRuntimeModeService.EvaluateLifecycle(Payload(intent: LicenseGrantIntent.Revoked), now)
            .ShouldBe(LicenseLifecycleState.Denied);
        LicenseRuntimeModeService.EvaluateLifecycle(Payload(), now, TrustedTimeHealth.RollbackDetected)
            .ShouldBe(LicenseLifecycleState.Denied);
        LicenseRuntimeModeService.EvaluateLifecycle(Payload(), now, bindingValid: false)
            .ShouldBe(LicenseLifecycleState.Denied);
    }

    [Fact]
    public void Evaluate_returns_grace_after_expiry_until_grace_period_ends()
    {
        var payload = Payload(expiresAt: new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero), gracePeriodDays: 2);

        LicenseRuntimeModeService.EvaluateLifecycle(payload, new DateTimeOffset(2026, 06, 19, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(LicenseLifecycleState.Grace);
    }

    [Fact]
    public void Evaluate_returns_readonly_after_grace_period()
    {
        var payload = Payload(expiresAt: new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero), gracePeriodDays: 2);

        LicenseRuntimeModeService.EvaluateLifecycle(payload, new DateTimeOffset(2026, 06, 21, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(LicenseLifecycleState.ReadOnly);
    }

    [Theory]
    [InlineData(null, 1, true)]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 2, false)]
    public void Revision_policy_requires_positive_strictly_newer_revision(
        int? existingRevision,
        int incomingRevision,
        bool expected)
    {
        LicenseRevisionPolicy.CanImport(existingRevision, incomingRevision).ShouldBe(expected);
    }

    private static LicensePayload Payload(
        LicenseGrantIntent intent = LicenseGrantIntent.Granted,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expiresAt = null,
        int gracePeriodDays = 0)
    {
        var issuedAt = new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero);
        return new LicensePayload(
            3,
            "dobby.plugin-license",
            "license-prod-2026-01",
            "license-1",
            "series-1",
            1,
            "plugin.sample",
            "default",
            "default",
            new string('a', 64),
            new string('b', 64),
            issuedAt,
            notBefore ?? issuedAt,
            expiresAt ?? issuedAt.AddDays(30),
            gracePeriodDays,
            intent);
    }
}
