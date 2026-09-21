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

    [Fact]
    public void Missing_license_state_denies_without_ever_deriving_the_current_device_fingerprint()
    {
        // `HostLoginRuntimeFactory` wires this seam through a `Lazy<Result<string>>` so the
        // `MachineGuid` registry read happens only when a license actually has to be checked
        // (`AC-014` measures login-path cost). The wiring itself is pinned by a source-text
        // assertion in `StartupBootstrapWiringTests`, which under `NFR-027` is a shape check and
        // not behaviour proof -- and which would stay green if a consumer kept the `Lazy<>` but
        // dereferenced it eagerly. This pins the consumer-side property instead: the no-license
        // path must not touch the seam.
        //
        // Two consequences make this more than a cost concern. The read can now fail closed
        // (`ADR-0063` § 5), so calling it where no license exists would turn "no license" into a
        // hardware error; and `Lazy<T>` caches a faulted evaluation, so one premature call on a
        // machine with an unreadable key would poison every later license evaluation in the
        // process.
        var store = new InMemoryLicenseStateStore();
        var derivations = 0;
        var provider = CreateProvider(store, fingerprintProvider: () =>
        {
            derivations++;
            return Result.Success(new string('a', 64));
        });

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PluginRuntimeMode.Denied);
        derivations.ShouldBe(0);
    }

    [Fact]
    public void Binding_check_derives_the_current_device_fingerprint_exactly_once()
    {
        // The counter-test to the one above: on a path that does have license state the seam
        // must actually be consulted, otherwise "never called" would be satisfiable by a
        // provider that ignores device binding altogether -- which is the failure mode
        // `ADR-0063` § 4 exists to prevent. Exactly once, not merely at least once: a
        // re-derivation per call would re-read the registry on every plugin evaluation.
        var store = new InMemoryLicenseStateStore
        {
            State = State(LicenseLifecycleState.Active)
        };
        var derivations = 0;
        var provider = CreateProvider(store, fingerprintProvider: () =>
        {
            derivations++;
            return Result.Success(new string('a', 64));
        });

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeTrue();
        derivations.ShouldBe(1);
    }

    [Fact]
    public void Unavailable_hardware_signal_denies_instead_of_throwing_or_using_a_placeholder()
    {
        // `ADR-0063` § 5 end-to-end at this consumer: a failed hardware read must become a
        // typed `LicenseBindingMismatch`, not an exception escaping toward the dispatcher and
        // not a placeholder digest compared against the persisted one.
        var store = new InMemoryLicenseStateStore
        {
            State = State(LicenseLifecycleState.Active)
        };
        var provider = CreateProvider(
            store,
            fingerprintProvider: () => Result.Failure<string>(ErrorCode.NotFound));

        var result = provider.GetCurrentMode("com.dobby.sample", "package");

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.LicenseBindingMismatch);
    }

    private static LicenseRuntimeModeProvider CreateProvider(
        InMemoryLicenseStateStore store,
        string currentDeviceFingerprint = null!,
        TimeProvider? timeProvider = null,
        Func<Result<string>>? fingerprintProvider = null)
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
            fingerprintProvider ?? (() => Result.Success(currentDeviceFingerprint)),
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
