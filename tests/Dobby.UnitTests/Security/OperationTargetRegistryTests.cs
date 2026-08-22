using Dobby.Core;
using Dobby.Security.Authorization;
using Shouldly;

namespace Dobby.UnitTests.Security;

public sealed class OperationTargetRegistryTests
{
    [Fact]
    public void Default_registry_registers_builtin_targets_with_supported_verbs_only()
    {
        var registry = OperationTargetRegistry.CreateDefault();

        var users = registry.Get(BuiltInOperationTargets.UserManagementTargetId).Value;
        users.TargetId.ShouldBe("users.management");
        users.SupportedVerbs.ShouldBe([
            OperationVerb.View,
            OperationVerb.Create,
            OperationVerb.Update,
            OperationVerb.Delete,
            OperationVerb.Execute,
            OperationVerb.Grant
        ], ignoreOrder: true);
        users.Supports(OperationVerb.Import).ShouldBeFalse("User Management delete gates lifecycle deactivate/reactivate, not hard delete or import.");

        registry.Get(BuiltInOperationTargets.PluginLicenseTargetId).Value
            .SupportedVerbs.ShouldBe([OperationVerb.View, OperationVerb.Import], ignoreOrder: true);
        registry.Get(BuiltInOperationTargets.PluginManagementTargetId).Value
            .SupportedVerbs.ShouldBe([OperationVerb.View, OperationVerb.Import, OperationVerb.Configure, OperationVerb.Delete], ignoreOrder: true);
        registry.Get(BuiltInOperationTargets.PersistenceMaintenanceTargetId).Value
            .SupportedVerbs.ShouldBe([OperationVerb.Export, OperationVerb.Import], ignoreOrder: true);
    }

    [Fact]
    public void Registry_has_no_explicit_deny_surface()
    {
        typeof(OperationTarget).GetProperties()
            .Select(property => property.Name)
            .ShouldNotContain("Deny");
        typeof(OperationTargetRegistry).GetMethods()
            .Select(method => method.Name)
            .ShouldNotContain("RegisterDeny");
    }

    [Fact]
    public void Unknown_target_and_unsupported_verb_fail_closed()
    {
        var registry = OperationTargetRegistry.CreateDefault();

        registry.Supports(new OperationKey("missing.target", OperationVerb.View)).ShouldBeFalse();
        registry.Supports(new OperationKey("users.management", OperationVerb.Import)).ShouldBeFalse();
    }

    [Fact]
    public void Plugin_targets_can_be_registered_from_validated_metadata_without_activation()
    {
        var registry = OperationTargetRegistry.CreateDefault();

        var result = registry.RegisterPluginTarget(
            "com.dobby.sample.greet",
            [OperationVerb.Execute, OperationVerb.View],
            "sample.greet.label");

        result.IsSuccess.ShouldBeTrue();
        var target = registry.Get("com.dobby.sample.greet").Value;
        target.Origin.ShouldBe(OperationTargetOrigin.Plugin);
        target.SupportedVerbs.ShouldBe([OperationVerb.Execute, OperationVerb.View], ignoreOrder: true);
    }

    [Fact]
    public void Plugin_target_registration_rejects_invalid_metadata()
    {
        var registry = OperationTargetRegistry.CreateDefault();

        registry.RegisterPluginTarget("", [OperationVerb.View], "label").ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        registry.RegisterPluginTarget("plugin.target", [], "label").ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        registry.RegisterPluginTarget("plugin.target", [OperationVerb.View], "").ErrorCode.ShouldBe(ErrorCode.InvalidInput);
    }
}
