using Dobby.Host;
using Shouldly;

namespace Dobby.IntegrationTests.Runtime;

public sealed class RuntimeFolderLayoutTests
{
    [Fact]
    public void Runtime_layout_matches_storage_spec_required_tree()
    {
        RuntimeFolderLayout.RequiredFolderNames.ShouldBe(
        [
            "data",
            "config",
            "logs",
            "plugins",
            "licenses",
            "recovery",
            "backup",
            "temp"
        ]);
    }
}
