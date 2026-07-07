using System.Globalization;
using Dobby.Host;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class WorkspaceTabSizingConvertersTests
{
    [Fact]
    public void Tab_strip_max_width_reserves_caption_control_space()
    {
        var converter = new WorkspaceTabStripMaxWidthConverter();

        var width = (double)converter.Convert([1024d, 42d], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(820d);
    }

    [Fact]
    public void Tab_width_uses_chrome_scale_maximum_when_space_allows()
    {
        var converter = new WorkspaceTabWidthConverter();

        var width = (double)converter.Convert([1600d, 4], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(240d);
    }

    [Fact]
    public void Tab_width_shrinks_evenly_when_many_tabs_share_the_strip()
    {
        var converter = new WorkspaceTabWidthConverter();

        var width = (double)converter.Convert([820d, 10], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(76d);
    }

    [Fact]
    public void Tab_width_keeps_a_close_button_readable_minimum()
    {
        var converter = new WorkspaceTabWidthConverter();

        var width = (double)converter.Convert([240d, 10], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(48d);
    }
}
