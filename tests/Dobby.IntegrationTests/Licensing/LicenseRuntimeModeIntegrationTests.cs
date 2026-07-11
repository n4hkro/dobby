using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Licensing;
using Dobby.Security.CurrentContext;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class LicenseRuntimeModeIntegrationTests
{
    [Fact]
    public void Plugin_capability_recomputes_runtime_mode_from_persisted_lifecycle_inputs()
    {
        var store = new RecordingLicenseStateStore
        {
            State = State(
                LicenseLifecycleState.Grace,
                sourceStatus: LicenseArtifactStatus.Active,
                notBeforeUtc: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                expiresAtUtc: DateTimeOffset.Parse("2026-06-10T00:00:00Z"),
                gracePeriodDays: 2)
        };
        var provider = CreateProvider(
            store,
            new RecordingCurrentContextStore(new CurrentCustomerDeploymentContext("default", "default")),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-13T00:00:00Z")));
        ICapabilityLicensing capability = new PluginCapabilityLicensing(
            "plugin.sample",
            new string('b', 64),
            provider);

        var mode = capability.GetCurrentMode();

        mode.IsSuccess.ShouldBeTrue();
        mode.Value.ShouldBe(PluginRuntimeMode.ReadOnlyExpired);
    }

    [Fact]
    public void Runtime_mode_fails_closed_before_plugin_visible_mode_when_current_context_is_missing()
    {
        var store = new RecordingLicenseStateStore
        {
            State = State(LicenseLifecycleState.Active)
        };
        var provider = CreateProvider(
            store,
            new RecordingCurrentContextStore(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-18T00:00:00Z")));

        var mode = provider.GetCurrentMode("plugin.sample", new string('b', 64));

        mode.IsSuccess.ShouldBeFalse();
        mode.ErrorCode.ShouldBe(ErrorCode.LicenseBindingMismatch);
    }

    private static LicenseRuntimeModeProvider CreateProvider(
        ILicenseStateStore store,
        ICurrentCustomerDeploymentContextStore contextStore,
        TimeProvider timeProvider)
    {
        return new LicenseRuntimeModeProvider(
            store,
            new CurrentCustomerDeploymentContextService(contextStore),
            () => new string('a', 64),
            timeProvider);
    }

    private static LicenseStateRecord State(
        LicenseLifecycleState lifecycleState,
        LicenseArtifactStatus? sourceStatus = null,
        DateTimeOffset? notBeforeUtc = null,
        DateTimeOffset? expiresAtUtc = null,
        int? gracePeriodDays = null)
    {
        return new LicenseStateRecord(
            new RevisionSeriesKey("plugin.sample", "default", "default"),
            "plugin.sample",
            "default",
            "default",
            new string('a', 64),
            new string('b', 64),
            7,
            lifecycleState,
            DateTimeOffset.Parse("2026-06-18T00:00:00Z"),
            notBeforeUtc,
            expiresAtUtc,
            gracePeriodDays,
            sourceStatus);
    }

    private sealed class RecordingLicenseStateStore : ILicenseStateStore
    {
        public LicenseStateRecord? State { get; set; }

        public LicenseStateRecord? Read(RevisionSeriesKey key)
        {
            return State is not null && State.RevisionSeriesKey == key ? State : null;
        }

        public void Save(LicenseStateRecord state)
        {
            State = state;
        }

        public void AppendAttestation(LicenseImportAttestation attestation)
        {
        }
    }

    private sealed class RecordingCurrentContextStore(params CurrentCustomerDeploymentContext[] contexts)
        : ICurrentCustomerDeploymentContextStore
    {
        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
        {
            return contexts;
        }

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
        }

        public void ProvisionContext(CurrentCustomerDeploymentContext context)
        {
        }
    }
}
