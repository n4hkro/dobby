using Dobby.Audit;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
using Dobby.UnitTests.PluginFramework.Packaging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class PluginRuntimeAuditCoordinatorTests
{
    [Fact]
    public void Install_attempt_and_verified_events_use_support_safe_package_and_plugin_details()
    {
        var trail = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(trail);
        var descriptor = CreateDescriptor();
        var correlationId = CorrelationId.New();

        coordinator.AppendInstallAttempt("owner", "C:/secret/path/sample.dobbyplugin", correlationId);
        coordinator.AppendInstallVerified("owner", descriptor, correlationId);

        var events = trail.Snapshot();
        events.Select(evt => evt.EventName).ShouldBe([
            AuditEventName.PluginInstallAttempted,
            AuditEventName.PluginInstallVerified
        ]);
        events[0].Target.ShouldBe("plugin-package");
        events[0].Details!.ContainsKey("packageName").ShouldBeFalse();
        events[1].Target.ShouldBe("plugin:com.dobby.sample");
        events[1].Details!["fingerprint"].ShouldBe(descriptor.Fingerprint);
    }

    [Theory]
    [InlineData("signature-failed", AuditEventName.PluginInstallSignatureFailed)]
    [InlineData("trust-failed", AuditEventName.PluginInstallTrustFailed)]
    [InlineData("forbidden-content", AuditEventName.PluginInstallRejected)]
    public void Install_rejections_map_to_required_plugin_audit_taxonomy_without_raw_messages(
        string reasonCode,
        string expectedEventName)
    {
        var trail = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(trail);

        coordinator.AppendInstallRejected(
            "owner",
            "com.dobby.sample",
            [new PluginPackageValidationReason(
                reasonCode,
                string.Concat("pass", "word=abc ", "private", "Key=def"),
                "signature")],
            CorrelationId.New());

        var auditEvent = trail.Snapshot().Single();
        auditEvent.EventName.ShouldBe(expectedEventName);
        auditEvent.Result.ShouldBe(AuditResult.Denied);
        auditEvent.Details!["reasonCode"].ShouldBe(reasonCode);
        auditEvent.Details!["diagnosticMarker"].ShouldBe($"{expectedEventName}:{reasonCode}");
        auditEvent.ToCanonicalPayload()["details"].ShouldNotContain(string.Concat("pass", "word=abc"));
        auditEvent.ToCanonicalPayload()["details"].ShouldNotContain(string.Concat("private", "Key=def"));
    }

    [Fact]
    public void Activation_outcomes_emit_only_skipped_rejected_and_faulted_audit_events()
    {
        var trail = new InMemoryAuditTrail();
        var coordinator = CreateCoordinator(trail);

        coordinator.AppendActivationOutcomes(
            "host",
            [
                new PluginActivationOutcome("active", PluginActivationStatus.Active, string.Empty),
                new PluginActivationOutcome("skipped", PluginActivationStatus.Skipped, "dependency-activation-failed: core"),
                new PluginActivationOutcome("rejected", PluginActivationStatus.Rejected, "host-version-below-min: required=2.0.0 installed=1.0.0"),
                new PluginActivationOutcome("faulted", PluginActivationStatus.Faulted, "PluginInternalError")
            ],
            CorrelationId.New());

        var events = trail.Snapshot();
        events.Select(evt => evt.EventName).ShouldBe([
            AuditEventName.PluginActivationSkipped,
            AuditEventName.PluginActivationRejected,
            AuditEventName.PluginRuntimeFaulted
        ]);
        events.Single(evt => evt.EventName == AuditEventName.PluginActivationSkipped)
            .Details!["reasonCode"].ShouldBe("dependency-activation-failed");
        events.Single(evt => evt.EventName == AuditEventName.PluginActivationRejected)
            .Result.ShouldBe(AuditResult.Denied);
        events.Single(evt => evt.EventName == AuditEventName.PluginRuntimeFaulted)
            .Result.ShouldBe(AuditResult.Failed);
    }

    private static PluginRuntimeAuditCoordinator CreateCoordinator(InMemoryAuditTrail trail)
    {
        return new PluginRuntimeAuditCoordinator(
            trail,
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-09T00:00:00Z")));
    }

    private static PluginPackageDescriptor CreateDescriptor()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var fingerprint = PluginPackageFingerprint.Compute(manifest);
        return new PluginPackageDescriptor(
            "sample.dobbyplugin",
            fingerprint.Fingerprint,
            fingerprint.CanonicalPayload,
            manifest,
            [],
            PluginPackageReader.ManifestFileName,
            PluginPackageTestData.CreateManifestJson());
    }
}
