using Dobby.Diagnostics;
using Shouldly;

namespace Dobby.UnitTests.Diagnostics;

public sealed class LocalizationMissingKeyEventTests
{
    [Fact]
    public void Localization_missing_key_event_carries_diagnostics_only_fields()
    {
        var evt = new LocalizationMissingKeyEvent(
            "az-AZ",
            "Plugins.InstallButton",
            "plugin:sample",
            "found-in-fallback");

        evt.EventName.ShouldBe("localization.missing-key");
        evt.DeduplicationKey.ShouldBe("az-AZ|Plugins.InstallButton|plugin:sample");
        evt.Severity.ShouldBe("Warning");
    }
}
