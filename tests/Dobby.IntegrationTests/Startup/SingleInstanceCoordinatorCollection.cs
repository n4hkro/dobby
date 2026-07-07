using Xunit;

namespace Dobby.IntegrationTests.Startup;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SingleInstanceCoordinatorCollection
{
    public const string Name = "Single instance coordinator";
}
