using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.PluginStorage;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Contracts;

public sealed class PluginContractDefaultsTests
{
    [Fact]
    public void PluginPageDescriptor_constructs_with_required_fields()
    {
        var descriptor = new PluginPageDescriptor(
            "sample.extension",
            "sample.route",
            "sample.label");

        descriptor.ExtensionPointId.ShouldBe("sample.extension");
        descriptor.RouteKey.ShouldBe("sample.route");
        descriptor.LabelKey.ShouldBe("sample.label");
    }

    [Fact]
    public void PluginPageContext_constructs_with_string_correlation_id()
    {
        var context = new PluginPageContext(
            "sample.plugin",
            "sample.extension",
            "sample.route",
            "correlation-id");

        context.PluginId.ShouldBe("sample.plugin");
        context.ExtensionPointId.ShouldBe("sample.extension");
        context.RouteKey.ShouldBe("sample.route");
        context.CorrelationId.ShouldBe("correlation-id");
    }

    [Fact]
    public void Plugin_component_page_model_carries_opaque_commands_without_delegates()
    {
        var commandContext = new PluginPageCommandContext(
            "sample.plugin",
            "sample.route",
            "managed",
            "correlation-id");
        var catalog = new PluginLocalizationCatalog(
            "en-US",
            [new PluginLocalizationBundle("en-US", new Dictionary<string, string>
            {
                ["title"] = "Title",
                ["failure"] = "Failed",
                ["cancelled"] = "Cancelled",
                ["body"] = "Body",
                ["command"] = "Run"
            })]);
        var model = new PluginComponentPageModel(
            commandContext,
            new PluginLocalizedText("title"),
            new PluginLocalizedText("failure"),
            new PluginLocalizedText("cancelled"),
            catalog,
            [new PluginTextComponent("body", new PluginLocalizedText("body"))],
            [new PluginCommandDescriptor(
                "sample.greet",
                new PluginLocalizedText("command"),
                "sample.plugin.greet.execute",
                CanCancel: false)]);

        model.Context.UserId.ShouldBe("managed");
        model.Commands.Single().CommandId.ShouldBe("sample.greet");
        model.Commands.Single().OperationKey.ShouldBe("sample.plugin.greet.execute");
        typeof(PluginCommandDescriptor).GetProperties()
            .Any(property => typeof(Delegate).IsAssignableFrom(property.PropertyType))
            .ShouldBeFalse();
        PluginComponentModelValidator.Validate(model).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void PolicyDecision_shape_matches_ADR_0004()
    {
        var type = typeof(PolicyDecision);

        type.IsValueType.ShouldBeTrue();
        type.GetProperty(nameof(PolicyDecision.Allowed)).ShouldNotBeNull();
        type.GetProperty(nameof(PolicyDecision.DenyReason)).ShouldNotBeNull();
        type.GetProperty("IsAllowed").ShouldBeNull();
    }

    [Fact]
    public void PolicyDecision_Allow_factory_produces_allowed_decision()
    {
        var decision = PolicyDecision.Allow();

        decision.Allowed.ShouldBeTrue();
        decision.DenyReason.ShouldBeNull();
    }

    [Fact]
    public void PolicyDecision_Deny_factory_carries_documented_reason_shape()
    {
        var decision = PolicyDecision.Deny("denied.by.policy");

        decision.Allowed.ShouldBeFalse();
        decision.DenyReason.ShouldBe("denied.by.policy");
    }

    [Fact]
    public void OperationPermissionCapability_shape_matches_ADR_0048_and_keeps_operation_key()
    {
        var capability = new OperationPermissionCapability("document.print");

        typeof(OperationPermissionCapability).IsValueType.ShouldBeTrue();
        capability.OperationKey.ShouldBe("document.print");
    }

    [Fact]
    public void IPluginStorage_exposes_two_compartment_properties()
    {
        var storageType = typeof(IPluginStorage);

        storageType.GetProperty(nameof(IPluginStorage.SharedDomainStorage))!
            .PropertyType.ShouldBe(typeof(IPluginStorageCompartment));
        storageType.GetProperty(nameof(IPluginStorage.UserScopedStorage))!
            .PropertyType.ShouldBe(typeof(IPluginStorageCompartment));
    }

    [Fact]
    public void PluginStorageKeyPrefix_Create_accepts_empty_and_rejects_invalid_prefixes()
    {
        PluginStorageKeyPrefix.Create("").IsSuccess.ShouldBeTrue();

        foreach (var invalidPrefix in new[] { "/absolute", "..", "../escape", "host:reserved" })
        {
            var result = PluginStorageKeyPrefix.Create(invalidPrefix);

            result.IsSuccess.ShouldBeFalse();
            result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        }
    }

    [Fact]
    public void PluginRuntimeMode_enum_has_exactly_three_documented_values()
    {
        Enum.GetValues<PluginRuntimeMode>()
            .ShouldBe([
                PluginRuntimeMode.FullAccess,
                PluginRuntimeMode.ReadOnlyExpired,
                PluginRuntimeMode.Denied,
            ]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void BackgroundOperationProgress_Determinate_rejects_out_of_range(int percent)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BackgroundOperationProgress.Determinate(percent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void BackgroundOperationProgress_Determinate_accepts_percent_range(int percent)
    {
        var progress = BackgroundOperationProgress.Determinate(percent);

        progress.Percent.ShouldBe(percent);
        progress.IsIndeterminate.ShouldBeFalse();
    }

    [Fact]
    public void BackgroundOperationProgress_Indeterminate_constructs_without_percent()
    {
        var progress = BackgroundOperationProgress.Indeterminate();

        progress.Percent.ShouldBeNull();
        progress.IsIndeterminate.ShouldBeTrue();
    }

    [Fact]
    public void BackgroundOperationSnapshot_carries_documented_payload()
    {
        var operationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var progress = BackgroundOperationProgress.Determinate(25);
        var snapshot = new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            "sample.plugin",
            "operation.display",
            "operation.detail",
            BackgroundOperationStatus.Failed,
            progress,
            CanCancel: true,
            correlationId,
            ErrorCode.PluginInternalError);

        snapshot.OperationId.ShouldBe(operationId);
        snapshot.OwnerKind.ShouldBe(BackgroundOperationOwnerKind.Plugin);
        snapshot.OwnerId.ShouldBe("sample.plugin");
        snapshot.DisplayNameKey.ShouldBe("operation.display");
        snapshot.ShortDetailKey.ShouldBe("operation.detail");
        snapshot.Status.ShouldBe(BackgroundOperationStatus.Failed);
        snapshot.Progress.ShouldBe(progress);
        snapshot.CanCancel.ShouldBeTrue();
        snapshot.CorrelationId.ShouldBe(correlationId);
        snapshot.FailureReason.ShouldBe(ErrorCode.PluginInternalError);
    }

    [Fact]
    public void ICapabilityLicensing_GetCurrentMode_returns_documented_runtime_mode()
    {
        ICapabilityLicensing licensing = new StubLicensing(PluginRuntimeMode.ReadOnlyExpired);

        var result = licensing.GetCurrentMode();

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PluginRuntimeMode.ReadOnlyExpired);
    }

    private sealed class StubLicensing(PluginRuntimeMode mode) : ICapabilityLicensing
    {
        public Result<PluginRuntimeMode> GetCurrentMode() => Result.Success(mode);
    }
}
