using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Shell.WorkSessions;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class ShellPluginWorkSessionServiceTests
{
    [Fact]
    public void Open_maps_exhausted_owner_capacity_to_resource_busy()
    {
        var registry = new WorkSessionRegistry();
        var service = new ShellPluginWorkSessionService(registry);
        for (var index = 0; index < WorkSessionRegistry.MaximumSessionsPerOwner; index++)
        {
            service.Open("com.dobby.sample", $"plugin/com.dobby.sample/work/{index}", "sample.work")
                .IsSuccess.ShouldBeTrue();
        }

        var denied = service.Open("com.dobby.sample", "plugin/com.dobby.sample/work/overflow", "sample.work");

        denied.IsSuccess.ShouldBeFalse();
        denied.ErrorCode.ShouldBe(ErrorCode.ResourceBusy);
        registry.Snapshot().Count.ShouldBe(WorkSessionRegistry.MaximumSessionsPerOwner);
    }

    [Fact]
    public void Oversize_arguments_map_to_invalid_input_without_mutating_the_session()
    {
        var registry = new WorkSessionRegistry();
        var service = new ShellPluginWorkSessionService(registry);
        var opened = service.Open("com.dobby.sample", "plugin/com.dobby.sample/work", "sample.work");
        opened.IsSuccess.ShouldBeTrue();
        var oversizeKey = new string('k', WorkSessionRegistry.MaximumKeyLength + 1);

        service.RecordResult(opened.Value.SessionId, oversizeKey).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        service.RecordError(opened.Value.SessionId, oversizeKey).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        service.UpdateProgress(opened.Value.SessionId, 101).ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        service.Open("com.dobby.sample", oversizeKey, "sample.work").ErrorCode.ShouldBe(ErrorCode.InvalidInput);

        var stored = registry.Get(opened.Value.SessionId).ShouldNotBeNull();
        stored.ResultSummaryKey.ShouldBeNull();
        stored.ErrorSummaryKey.ShouldBeNull();
        stored.Progress.IsIndeterminate.ShouldBeTrue();
        registry.Snapshot().Count.ShouldBe(1);
    }

    [Fact]
    public void Close_owned_by_removes_only_the_requested_owner_sessions()
    {
        var registry = new WorkSessionRegistry();
        var service = new ShellPluginWorkSessionService(registry);
        var target = service.Open("com.dobby.sample", "plugin/com.dobby.sample/work", "sample.work");
        var survivor = service.Open("com.dobby.other", "plugin/com.dobby.other/work", "other.work");

        service.CloseOwnedBy("com.dobby.sample").IsSuccess.ShouldBeTrue();

        registry.Get(target.Value.SessionId).ShouldBeNull();
        registry.Get(survivor.Value.SessionId).ShouldNotBeNull();
    }
}
