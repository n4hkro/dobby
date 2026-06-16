using Dobby.Host.Plugins;
using Dobby.PluginFramework.Activation;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class PluginRuntimeDiagnosticsCoordinatorTests
{
    [Fact]
    public void Append_activation_failure_marker_records_redacted_technical_diagnostics()
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);

        coordinator.RecordActivationOutcome(new PluginActivationOutcome(
            "com.dobby.sample",
            PluginActivationStatus.Faulted,
            string.Concat("PluginInternalError pass", "word=abc private", "Key=def")));

        var entry = entries.Single();
        entry.PluginId.ShouldBe("com.dobby.sample");
        entry.Marker.ShouldBe("plugin.runtime.faulted:PluginInternalError");
        entry.Details.ShouldNotContain(string.Concat("pass", "word=abc"));
        entry.Details.ShouldNotContain(string.Concat("private", "Key=def"));
    }

    [Fact]
    public void Active_activation_outcomes_do_not_emit_technical_diagnostics()
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);

        coordinator.RecordActivationOutcome(new PluginActivationOutcome(
            "com.dobby.sample",
            PluginActivationStatus.Active,
            string.Empty));

        entries.ShouldBeEmpty();
    }
}
