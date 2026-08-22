using Dobby.Core;

namespace Dobby.Audit;

public interface IHostMutationUnitOfWork
{
    T Execute<T>(Func<IHostMutationContext, T> action);

    void Execute(Action<IHostMutationContext> action);

    Result<T> ExecuteResult<T>(Func<IHostMutationContext, Result<T>> action);

    Result ExecuteResult(Func<IHostMutationContext, Result> action);
}
