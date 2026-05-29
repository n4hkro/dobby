using Xunit;

namespace Dobby.IntegrationTests.Auth;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OwnerBootstrapConcurrencyCollection
{
    public const string Name = "Owner bootstrap concurrency";
}
