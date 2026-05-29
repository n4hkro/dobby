using System.IO.Compression;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host;
using Shouldly;

namespace Dobby.IntegrationTests.Host;

public sealed class ShellDiagnosticsExportActionTests
{
    [Fact]
    public void Authenticated_shell_action_builds_local_redacted_export_and_audits_existing_event()
    {
        using var fixture = DiagnosticsExportFixture.Create();
        var audit = new InMemoryAuditTrail();
        var coordinator = new ShellDiagnosticsExportCoordinator(
            new DiagnosticsExportPackageBuilder(),
            audit,
            TimeProvider.System);
        var correlationId = CorrelationId.New();

        var result = coordinator.Export(
            "owner-1",
            fixture.SourceDirectory,
            fixture.OutputZipPath,
            correlationId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Uploaded.ShouldBeFalse();
        File.Exists(fixture.OutputZipPath).ShouldBeTrue();
        using (var archive = ZipFile.OpenRead(fixture.OutputZipPath))
        {
            archive.Entries.Select(entry => entry.FullName).ShouldContain("diagnostics.log");
            archive.Entries.Select(entry => entry.FullName).ShouldNotContain("passwords.txt");
            using var entry = archive.GetEntry("diagnostics.log")!.Open();
            using var reader = new StreamReader(entry);
            var redacted = reader.ReadToEnd();
            redacted.ShouldContain("<redacted>", Case.Sensitive);
            redacted.ShouldNotContain("abc", Case.Sensitive);
        }

        var auditEvent = audit.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.DiagnosticsExportCreated);
        auditEvent.ActorId.ShouldBe("owner-1");
        auditEvent.CorrelationId.ShouldBe(correlationId);
        auditEvent.Details!["uploaded"].ShouldBe("false");
    }

    private sealed class DiagnosticsExportFixture : IDisposable
    {
        private DiagnosticsExportFixture(string root)
        {
            Root = root;
            SourceDirectory = Path.Combine(root, "source");
            Directory.CreateDirectory(SourceDirectory);
            File.WriteAllText(Path.Combine(SourceDirectory, "diagnostics.log"), "token=abc");
            File.WriteAllText(Path.Combine(SourceDirectory, "passwords.txt"), "secret");
            OutputZipPath = Path.Combine(root, "dobby-diagnostics.zip");
        }

        public string Root { get; }

        public string SourceDirectory { get; }

        public string OutputZipPath { get; }

        public static DiagnosticsExportFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-shell-diagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new DiagnosticsExportFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
