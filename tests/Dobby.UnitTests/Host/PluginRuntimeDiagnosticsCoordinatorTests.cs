using System.Diagnostics;
using Dobby.Contracts.Events;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Packaging;
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
    public void Install_rejection_marker_records_redacted_package_validation_diagnostics()
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);

        coordinator.RecordInstallRejected(
            "com.dobby.sample",
            [
                new PluginPackageValidationReason(
                    "forbidden-content",
                    string.Concat("Package rejected pass", "word=abc private", "Key=def"),
                    "customer-data/export.json")
            ]);

        var entry = entries.Single();
        entry.PluginId.ShouldBe("com.dobby.sample");
        entry.Marker.ShouldBe("plugin.install.rejected:forbidden-content");
        entry.Details.ShouldContain("customer-data/export.json");
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

    [Fact]
    public void Event_handler_fault_marker_uses_faulting_plugin_id()
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);

        coordinator.RecordEventHandlerFault(
            "com.dobby.event-fault",
            typeof(SampleEvent),
            ErrorCode.PluginInternalError);

        var entry = entries.Single();
        entry.PluginId.ShouldBe("com.dobby.event-fault");
        entry.Marker.ShouldBe("plugin.runtime.defect:plugin.event.handler.pluginexception");
        entry.Details.ShouldContain("SampleEvent");
        entry.Details.ShouldContain(nameof(ErrorCode.PluginInternalError));
    }

    [Fact]
    public void Page_and_action_fault_markers_record_safe_route_and_error_details()
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);

        coordinator.RecordPageFault(
            "com.dobby.sample",
            "plugin/com.dobby.sample/sample.home",
            "load",
            ErrorCode.PluginInternalError);
        coordinator.RecordPageActionFault(
            "com.dobby.sample",
            "plugin/com.dobby.sample/sample.home",
            "sample.action",
            ErrorCode.PluginInternalError);

        entries[0].PluginId.ShouldBe("com.dobby.sample");
        entries[0].Marker.ShouldBe("plugin.runtime.defect:plugin.page.load.plugindeclaredfailure");
        entries[0].Details.ShouldContain("plugin/com.dobby.sample/sample.home");
        entries[0].Details.ShouldContain(nameof(ErrorCode.PluginInternalError));
        entries[1].PluginId.ShouldBe("com.dobby.sample");
        entries[1].Marker.ShouldBe("plugin.runtime.defect:plugin.page.command.plugindeclaredfailure");
        entries[1].Details.ShouldContain("sample.action");
        entries[1].Details.ShouldContain(nameof(ErrorCode.PluginInternalError));
    }

    [Theory]
    [MemberData(nameof(ExpectedOutcomes))]
    public void Expected_page_command_outcomes_do_not_emit_fault_diagnostics(
        ErrorCode errorCode,
        PluginInvocationOutcomeKind outcomeKind)
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);

        for (var index = 0; index < 5; index++)
        {
            coordinator.RecordPageActionOutcome(
                "com.dobby.sample",
                "plugin/com.dobby.sample/greet",
                "sample.submit",
                errorCode,
                outcomeKind,
                Guid.Empty.ToString());
        }

        entries.ShouldBeEmpty();
    }

    [Fact]
    public void Host_infrastructure_failure_is_attributed_to_host_with_stable_correlation()
    {
        var entries = new List<PluginRuntimeDiagnosticEntry>();
        var coordinator = new PluginRuntimeDiagnosticsCoordinator(entries.Add);
        var correlationId = Guid.CreateVersion7().ToString();

        coordinator.RecordPageActionOutcome(
            "com.dobby.sample",
            "plugin/com.dobby.sample/greet",
            "sample.submit",
            ErrorCode.DatabaseUnavailable,
            PluginInvocationOutcomeKind.HostInfrastructureFailure,
            correlationId);

        var entry = entries.Single();
        entry.PluginId.ShouldBe("host");
        entry.Marker.ShouldBe("plugin.runtime.host-failed:plugin.page.command");
        entry.Details.ShouldContain("pluginId=com.dobby.sample");
        entry.Details.ShouldContain(correlationId);
    }

    public static IEnumerable<object[]> ExpectedOutcomes()
    {
        yield return [ErrorCode.InvalidInput, PluginInvocationOutcomeKind.ExpectedRejection];
        yield return [ErrorCode.Forbidden, PluginInvocationOutcomeKind.AuthorizationOrLicenseDenied];
        yield return [ErrorCode.LicenseReadOnly, PluginInvocationOutcomeKind.AuthorizationOrLicenseDenied];
        yield return [ErrorCode.Cancelled, PluginInvocationOutcomeKind.Cancelled];
    }

    [Fact]
    public void File_sink_persists_redacted_plugin_runtime_diagnostics()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dobby-plugin-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new PluginRuntimeDiagnosticsFileSink(directory);

            sink.Record(new PluginRuntimeDiagnosticEntry(
                "com.dobby.sample",
                "plugin.runtime.failed:plugin.event.handler",
                string.Concat("event fault pass", "word=abc private", "Key=def")));

            var path = Path.Combine(directory, PluginRuntimeDiagnosticsFileSink.FileName);
            File.Exists(path).ShouldBeTrue();
            var contents = File.ReadAllText(path);
            contents.ShouldContain("pluginId=com.dobby.sample");
            contents.ShouldContain("marker=plugin.runtime.failed:plugin.event.handler");
            contents.ShouldNotContain(string.Concat("pass", "word=abc"));
            contents.ShouldNotContain(string.Concat("private", "Key=def"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task File_sink_serializes_concurrent_records_without_losing_entries()
    {
        const int workerCount = 64;
        const int entryCount = 2000;
        var directory = Path.Combine(Path.GetTempPath(), "dobby-plugin-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new PluginRuntimeDiagnosticsFileSink(directory);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workersReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var readyCount = 0;
            var workers = Enumerable.Range(0, workerCount)
                .Select(worker => Task.Run(async () =>
                {
                    if (Interlocked.Increment(ref readyCount) == workerCount)
                    {
                        workersReady.SetResult();
                    }

                    await start.Task;
                    for (var entryIndex = worker; entryIndex < entryCount; entryIndex += workerCount)
                    {
                        sink.Record(new PluginRuntimeDiagnosticEntry(
                            $"com.dobby.concurrent.{entryIndex}",
                            "plugin.runtime.failed:plugin.event.handler",
                            $"entry={entryIndex}"));
                    }
                }))
                .ToArray();

            await workersReady.Task;
            start.SetResult();
            await Task.WhenAll(workers);

            var path = Path.Combine(directory, PluginRuntimeDiagnosticsFileSink.FileName);
            File.ReadLines(path).Count().ShouldBe(entryCount);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void File_sink_emits_a_redacted_trace_fallback_when_writing_fails()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "dobby-plugin-diagnostics-" + Guid.NewGuid().ToString("N"));
        using var listener = new RecordingTraceListener();
        try
        {
            File.WriteAllText(filePath, "not a diagnostics directory");
            Trace.Listeners.Add(listener);
            var sink = new PluginRuntimeDiagnosticsFileSink(filePath);

            Should.NotThrow(() => sink.Record(new PluginRuntimeDiagnosticEntry(
                "com.dobby.sample",
                "plugin.runtime.failed:plugin.event.handler",
                string.Concat("exception pass", "word=abc private", "Key=def"))));

            listener.Messages
                .Where(message => message.StartsWith("plugin.runtime.diagnostics", StringComparison.Ordinal))
                .ShouldBe(["plugin.runtime.diagnostics.write.failed"]);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private sealed record SampleEvent(string Payload) : PublicIntegrationEvent;

    private sealed class RecordingTraceListener : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
            if (message is not null)
            {
                Messages.Add(message);
            }
        }

        public override void WriteLine(string? message)
        {
            Write(message);
        }
    }
}
