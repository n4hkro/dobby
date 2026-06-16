using Dobby.Contracts.PluginPages;
using Dobby.Navigation;
using Shouldly;

namespace Dobby.UnitTests.Navigation;

public sealed class PluginNavigationContributionMapperTests
{
    [Fact]
    public void Maps_contract_navigation_rows_to_host_normalized_contributions()
    {
        var mapped = PluginNavigationContributionMapper.Map(
        [
            new PluginNavigationContribution(
                "plugin.sample",
                "sample.reports",
                "plugin.sample.reports",
                "Dobby.Plugin.Sample.Reports",
                "top-toolbar",
                null,
                "default",
                25,
                0),
            new PluginNavigationContribution(
                "plugin.sample",
                "sample.reports.daily",
                "plugin.sample.reports.daily",
                "Dobby.Plugin.Sample.Reports.Daily",
                "sidebar",
                "sample.reports",
                "default",
                10,
                1)
        ]);

        mapped.Select(item => item.Id).ShouldBe(["sample.reports", "sample.reports.daily"]);
        mapped[0].Target.ShouldBe(NavigationTarget.TopToolbar);
        mapped[1].Target.ShouldBe(NavigationTarget.Sidebar);
        mapped[0].Ordering.PluginId.ShouldBe("plugin.sample");
        mapped[1].ParentId.ShouldBe("sample.reports");
    }

    [Fact]
    public void Map_normalizes_plugin_routes_and_uses_default_ordering_values()
    {
        var mapped = PluginNavigationContributionMapper.Map(
        [
            new PluginNavigationContribution(
                "com.dobby.sample",
                "sample.home",
                "sample.home",
                "Sample Plugin",
                "top-toolbar",
                null,
                null,
                null,
                0)
        ]);

        var item = mapped.Single();
        item.RouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
        item.LabelKey.ShouldBe("Sample Plugin");
        item.Ordering.Group.ShouldBe("default");
        item.Ordering.Weight.ShouldBe(100);
    }

    [Fact]
    public void Map_keeps_already_normalized_plugin_routes_canonical()
    {
        var mapped = PluginNavigationContributionMapper.Map(
        [
            new PluginNavigationContribution(
                "com.dobby.sample",
                "sample.home",
                "plugin/com.dobby.sample/sample.home",
                "Sample Plugin",
                "top-toolbar",
                null,
                "default",
                100,
                0)
        ]);

        mapped.Single().RouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
    }
}
