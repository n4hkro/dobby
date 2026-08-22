using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host;
using Dobby.Host.Startup;
using Shouldly;

namespace Dobby.IntegrationTests.Runtime;

public sealed class RuntimeFolderRecoveryTests
{
    [Fact]
    public void Runtime_folder_preparer_creates_required_tree()
    {
        using var temp = new TemporaryDirectory();
        var preparer = new RuntimeFolderPreparer();

        preparer.Prepare(new RuntimeOptions { DataRoot = temp.Path }, CorrelationId.New());

        foreach (var folder in RuntimeFolderLayout.RequiredFolderNames)
        {
            Directory.Exists(Path.Combine(temp.Path, folder)).ShouldBeTrue(folder);
        }

        var protectedDirectory = Path.Combine(temp.Path, "data", "protected");
        File.Exists(Path.Combine(protectedDirectory, "app-entropy.v1.dpapi")).ShouldBeTrue();
        File.Exists(Path.Combine(protectedDirectory, "stable-device-id.v1.dpapi")).ShouldBeTrue();
        File.Exists(Path.Combine(protectedDirectory, "db-open-secret.v1.dpapi")).ShouldBeTrue();
        File.Exists(Path.Combine(protectedDirectory, "protected-material.initialized.json")).ShouldBeTrue();
    }

    [Fact]
    public void Runtime_folder_preparer_fails_closed_when_protected_material_prerequisite_is_missing()
    {
        using var temp = new TemporaryDirectory();
        var preparer = new RuntimeFolderPreparer(() => false);

        Should.Throw<InvalidOperationException>(() =>
            preparer.Prepare(new RuntimeOptions { DataRoot = temp.Path }, CorrelationId.New()))
            .Message.ShouldContain("protected", Case.Insensitive);
    }

    [Fact]
    public void Temporary_output_recovery_is_canonical_bounded_and_idempotent()
    {
        using var temp = new TemporaryDirectory();
        var temporaryRoot = Path.Combine(temp.Path, "temp");
        var nested = Path.Combine(temporaryRoot, "nested");
        Directory.CreateDirectory(nested);
        var partial = Path.Combine(temporaryRoot, "dobby-diagnostics.zip.partial");
        var interrupted = Path.Combine(nested, "export.partial.019d0c4e");
        var completed = Path.Combine(temporaryRoot, "completed.zip");
        var outside = Path.Combine(temp.Path, "outside.tmp");
        File.WriteAllText(partial, "partial");
        File.WriteAllText(interrupted, "partial");
        File.WriteAllText(completed, "complete");
        File.WriteAllText(outside, "outside");
        var recovery = new RuntimeTemporaryOutputRecovery(temporaryRoot);

        var first = recovery.Recover();
        var second = recovery.Recover();

        first.IsSuccess.ShouldBeTrue();
        first.Value.ShouldBe(2);
        second.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBe(0);
        File.Exists(completed).ShouldBeTrue();
        File.Exists(outside).ShouldBeTrue();
    }
}
