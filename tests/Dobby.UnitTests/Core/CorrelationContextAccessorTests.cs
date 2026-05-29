using Dobby.Core;
using Shouldly;

namespace Dobby.UnitTests.Core;

public sealed class CorrelationContextAccessorTests
{
    [Fact]
    public void Generator_returns_unique_correlation_ids()
    {
        ICorrelationIdGenerator generator = new CorrelationIdGenerator();

        var first = generator.New();
        var second = generator.New();

        first.ShouldNotBe(second);
        first.Value.ShouldNotBe(Guid.Empty);
        second.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Accessor_returns_current_value_within_same_async_flow()
    {
        var accessor = new CorrelationContextAccessor();
        var correlationId = CorrelationId.New();

        try
        {
            accessor.Current = correlationId;

            accessor.Current.ShouldBe(correlationId);
        }
        finally
        {
            accessor.Current = null;
        }
    }

    [Fact]
    public async Task Accessor_propagates_across_awaited_task_yield()
    {
        var accessor = new CorrelationContextAccessor();
        var correlationId = CorrelationId.New();

        try
        {
            accessor.Current = correlationId;

            await Task.Yield();

            accessor.Current.ShouldBe(correlationId);
        }
        finally
        {
            accessor.Current = null;
        }
    }

    [Fact]
    public void Accessor_setter_rejects_existing_value_instead_of_overwriting()
    {
        var accessor = new CorrelationContextAccessor();
        var existing = CorrelationId.New();
        var replacement = CorrelationId.New();

        try
        {
            accessor.Current = existing;

            Should.Throw<InvalidOperationException>(() => accessor.Current = replacement);

            accessor.Current.ShouldBe(existing);
        }
        finally
        {
            accessor.Current = null;
        }
    }

    [Fact]
    public void Accessor_returns_null_when_no_value_set()
    {
        var accessor = new CorrelationContextAccessor
        {
            Current = null,
        };

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void PushScope_restores_previous_value_on_dispose()
    {
        var accessor = new CorrelationContextAccessor
        {
            Current = null,
        };
        var correlationId = CorrelationId.New();

        using (accessor.PushScope(correlationId))
        {
            accessor.Current.ShouldBe(correlationId);
        }

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void PushScope_rejects_existing_value_instead_of_overwriting()
    {
        var accessor = new CorrelationContextAccessor();
        var existing = CorrelationId.New();
        var replacement = CorrelationId.New();

        try
        {
            accessor.Current = existing;

            Should.Throw<InvalidOperationException>(() => accessor.PushScope(replacement));

            accessor.Current.ShouldBe(existing);
        }
        finally
        {
            accessor.Current = null;
        }
    }
}
