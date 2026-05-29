namespace Dobby.Infrastructure.Persistence;

public interface IHostPersistenceCommandExecutor
{
    int Execute(string sql, object? parameters = null);

    IReadOnlyList<T> Query<T>(string sql, object? parameters = null);

    T QueryScalar<T>(string sql, object? parameters = null);
}
