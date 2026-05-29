using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host;
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
}
