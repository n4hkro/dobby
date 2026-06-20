using System.Text.Json;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class SamplePluginManifestTests
{
    [Fact]
    public void Sample_plugin_manifest_matches_dev_qa_verification_artifact_contract()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SampleManifestPath()));
        var root = document.RootElement;

        root.TryGetProperty("schemaVersion", out _).ShouldBeFalse();
        root.GetProperty("pluginId").GetString().ShouldBe("com.dobby.sample");
        root.GetProperty("vendorId").GetString().ShouldBe("dobby.dev-qa");
        root.GetProperty("version").GetString().ShouldBe("1.0.0");
        root.GetProperty("entryAssembly").GetString().ShouldBe("bin/plugins/Dobby.Sample.Plugin.dll");
        root.GetProperty("displayName").GetString().ShouldBe("Dobby Sample");
        root.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("icon").GetProperty("path").GetString().ShouldBe("assets/sample-plugin.png");

        var operationTarget = root.GetProperty("operationTargets").EnumerateArray().Single();
        operationTarget.GetProperty("id").GetString().ShouldBe("com.dobby.sample.greet");
        operationTarget.GetProperty("verbs").EnumerateArray().Select(item => item.GetString()).ShouldBe(["execute"]);
        operationTarget.GetProperty("labelKey").GetString().ShouldBe("sample.greet.label");

        var extensionPoints = root.GetProperty("extensionPoints").EnumerateArray().ToArray();
        extensionPoints.Length.ShouldBe(2);
        extensionPoints[0].GetProperty("target").GetString().ShouldBe("top-toolbar");
        extensionPoints[0].GetProperty("routeKey").GetString().ShouldBe("sample");
        extensionPoints[1].GetProperty("target").GetString().ShouldBe("sidebar");
        extensionPoints[1].GetProperty("parentId").GetString().ShouldBe(extensionPoints[0].GetProperty("id").GetString());
        extensionPoints[1].GetProperty("routeKey").GetString().ShouldBe("greet");

        var files = root.GetProperty("files").EnumerateArray().ToDictionary(
            item => item.GetProperty("path").GetString()!,
            item => item.GetProperty("sha256").GetString()!,
            StringComparer.Ordinal);
        files.Keys.ShouldContain("bin/plugins/Dobby.Sample.Plugin.dll");
        files.Keys.ShouldContain("assets/sample-plugin.png");
        files.Values.ShouldAllBe(hash => hash.Length == 64 && hash.All(Uri.IsHexDigit));
        var signatureValue = root.GetProperty("signature").GetProperty("value").GetString();
        signatureValue.ShouldNotBeNull();
        signatureValue!.ShouldNotContain("skeleton");
    }

    private static string SampleManifestPath()
    {
        return Path.Combine(
            RepoRoot().FullName,
            "samples",
            "Dobby.Sample.Plugin",
            "dobby-plugin.manifest.json");
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
