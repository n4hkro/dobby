using Xunit;

namespace Dobby.IntegrationTests.Shell;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfIntegrationTestCollection
{
    public const string Name = "WPF integration tests";
}
