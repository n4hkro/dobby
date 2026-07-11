using Dobby.Contracts.Licensing;
using Dobby.Core;
using Dobby.Licensing;
using Dobby.Security.CurrentContext;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseRuntimeModeProviderTests
{
    [Fact]
    public void Get_current_mode_returns_denied_when_license_state_is_missing()
    {
        var store = new InMemoryLicenseStateStore();
        var provider = CreateProvider(store);

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PluginRuntimeMode.Denied);
    }

    [Fact]
    public void Get_current_mode_maps_saved_lifecycle_when_bindings_match()
    {
        var store = new InMemoryLicenseStateStore
        {
            State = State(LicenseLifecycleState.ReadOnly)
        };
        var provider = CreateProvider(store);

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PluginRuntimeMode.ReadOnlyExpired);
    }

    [Fact]
    public void Get_current_mode_recomputes_pending_license_after_not_before_arrives()
    {
        var store = new InMemoryLicenseStateStore
        {
            State = State(
                LicenseLifecycleState.Pending,
                sourceStatus: LicenseArtifactStatus.Active,
                notBeforeUtc: DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
                expiresAtUtc: DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                gracePeriodDays: 0)
        };
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T00:00:00Z"));
        var provider = CreateProvider(store, timeProvider: time);

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PluginRuntimeMode.FullAccess);
    }

    [Fact]
    public void Get_current_mode_recomputes_active_license_to_readonly_after_grace_ends()
    {
        var store = new InMemoryLicenseStateStore
        {
            State = State(
                LicenseLifecycleState.Grace,
                sourceStatus: LicenseArtifactStatus.Active,
                notBeforeUtc: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                expiresAtUtc: DateTimeOffset.Parse("2026-06-10T00:00:00Z"),
                gracePeriodDays: 2)
        };
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-13T00:00:00Z"));
        var provider = CreateProvider(store, timeProvider: time);

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PluginRuntimeMode.ReadOnlyExpired);
    }

    [Fact]
    public void Get_current_mode_fails_closed_when_package_binding_changes()
    {
        var store = new InMemoryLicenseStateStore
        {
            State = State(LicenseLifecycleState.Active)
        };
        var provider = CreateProvider(store);

        var result = provider.GetCurrentMode("com.dobby.sample", "changed-package");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.LicenseBindingMismatch);
    }

    [Fact]
    public void Get_current_mode_fails_closed_when_current_device_binding_changes()
    {
        var store = new InMemoryLicenseStateStore
        {
            State = State(LicenseLifecycleState.Active)
        };
        var provider = CreateProvider(store, currentDeviceFingerprint: "changed-device");

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.LicenseBindingMismatch);
    }

    private static LicenseRuntimeModeProvider CreateProvider(
        InMemoryLicenseStateStore store,
        string currentDeviceFingerprint = null!,
        TimeProvider? timeProvider = null)
    {
        currentDeviceFingerprint ??= new string('a', 64);
        timeProvider ??= new FakeTimeProvider(DateTimeOffset.Parse("2026-06-18T00:00:00Z"));
        var contextStore = new InMemoryCurrentContextStore(
        [
            new CurrentCustomerDeploymentContext("default", "default")
        ]);
        return new LicenseRuntimeModeProvider(
            store,
            new CurrentCustomerDeploymentContextService(contextStore),
            () => currentDeviceFingerprint,
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
            new RevisionSeriesKey("com.dobby.sample", "default", "default"),
            "com.dobby.sample",
            "default",
            "default",
            new string('a', 64),
            "package",
            7,
            lifecycleState,
            DateTimeOffset.Parse("2026-06-18T00:00:00Z"),
            notBeforeUtc,
            expiresAtUtc,
            gracePeriodDays,
            sourceStatus);
    }

    private sealed class InMemoryLicenseStateStore : ILicenseStateStore
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

    private sealed class InMemoryCurrentContextStore(
        IReadOnlyList<CurrentCustomerDeploymentContext> contexts) : ICurrentCustomerDeploymentContextStore
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
