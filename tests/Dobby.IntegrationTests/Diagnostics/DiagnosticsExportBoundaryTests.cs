using System.IO.Compression;
using Dobby.Diagnostics;
using Shouldly;

namespace Dobby.IntegrationTests.Diagnostics;

public sealed class DiagnosticsExportBoundaryTests
{
    [Fact]
    public void Diagnostics_export_package_redacts_allowed_logs_and_excludes_secret_artifacts()
    {
        using var temp = new TemporaryDirectory();
        var source = Path.Combine(temp.Path, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "dobby.log"), "password=secret safe=value");
        File.WriteAllText(Path.Combine(source, "token.txt"), "raw-token");
        File.WriteAllText(Path.Combine(source, "plugin-runtime-diagnostics.log"), "plugin domain details");
        var output = Path.Combine(temp.Path, "support.zip");

        var package = new DiagnosticsExportPackageBuilder().Build(source, output);

        package.Uploaded.ShouldBeFalse();
        package.IncludedEntries.ShouldBe(["dobby.log"]);
        package.ExcludedEntries.ShouldContain("token.txt");
        package.ExcludedEntries.ShouldContain("plugin-runtime-diagnostics.log");

        using var archive = ZipFile.OpenRead(output);
        var entry = archive.GetEntry("dobby.log");
        entry.ShouldNotBeNull();
        using var reader = new StreamReader(entry.Open());
        var contents = reader.ReadToEnd();
        contents.ShouldContain("password=<redacted>");
        contents.ShouldNotContain("secret");
    }
}
