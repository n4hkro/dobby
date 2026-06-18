using Dobby.Security.CurrentContext;
using Shouldly;

namespace Dobby.UnitTests.Security.CurrentContext;

public sealed class CurrentCustomerDeploymentContextTests
{
    [Fact]
    public void Ensure_default_context_materializes_default_when_store_is_empty()
    {
        var store = new InMemoryCurrentContextStore();
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.EnsureDefaultContext();

        result.IsSuccess.ShouldBeTrue();
        result.Context.ShouldBe(new CurrentCustomerDeploymentContext("default", "default"));
        store.SeedCount.ShouldBe(1);
        store.Contexts.ShouldBe([new CurrentCustomerDeploymentContext("default", "default")]);
    }

    [Fact]
    public void Ensure_default_context_is_idempotent_when_default_already_exists()
    {
        var store = new InMemoryCurrentContextStore(
            new CurrentCustomerDeploymentContext("default", "default"));
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.EnsureDefaultContext();

        result.IsSuccess.ShouldBeTrue();
        store.SeedCount.ShouldBe(0);
    }

    [Fact]
    public void Get_current_context_fails_closed_without_seeding_during_import_or_activation()
    {
        var store = new InMemoryCurrentContextStore();
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.GetCurrentContext();

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("binding-mismatch");
        result.SupportCode.ShouldBe("missing-current-context");
        store.SeedCount.ShouldBe(0);
    }

    [Fact]
    public void Get_current_context_fails_closed_for_duplicate_contexts()
    {
        var store = new InMemoryCurrentContextStore(
            new CurrentCustomerDeploymentContext("default", "default"),
            new CurrentCustomerDeploymentContext("default", "default"));
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.GetCurrentContext();

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("binding-mismatch");
        result.SupportCode.ShouldBe("duplicate-current-context");
    }

    [Fact]
    public void Validate_match_returns_non_disclosing_binding_mismatch_for_wrong_artifact_context()
    {
        var store = new InMemoryCurrentContextStore(
            new CurrentCustomerDeploymentContext("default", "default"));
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.ValidateMatches("customer-x", "deployment-y");

        result.IsSuccess.ShouldBeFalse();
        result.ReasonCode.ShouldBe("binding-mismatch");
        result.SupportCode.ShouldBe("context-binding-mismatch");
        result.SupportCode.ShouldNotContain("customer-x");
        result.SupportCode.ShouldNotContain("deployment-y");
    }

    [Fact]
    public void Validate_match_succeeds_for_existing_default_context()
    {
        var store = new InMemoryCurrentContextStore(
            new CurrentCustomerDeploymentContext("default", "default"));
        var service = new CurrentCustomerDeploymentContextService(store);

        var result = service.ValidateMatches("default", "default");

        result.IsSuccess.ShouldBeTrue();
        result.Context.ShouldBe(new CurrentCustomerDeploymentContext("default", "default"));
    }

    private sealed class InMemoryCurrentContextStore : ICurrentCustomerDeploymentContextStore
    {
        private readonly List<CurrentCustomerDeploymentContext> contexts;

        public InMemoryCurrentContextStore(params CurrentCustomerDeploymentContext[] contexts)
        {
            this.contexts = contexts.ToList();
        }

        public int SeedCount { get; private set; }

        public IReadOnlyList<CurrentCustomerDeploymentContext> Contexts => contexts;

        public IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts()
        {
            return contexts;
        }

        public void SeedDefaultContext(CurrentCustomerDeploymentContext context)
        {
            SeedCount++;
            contexts.Add(context);
        }
    }
}
