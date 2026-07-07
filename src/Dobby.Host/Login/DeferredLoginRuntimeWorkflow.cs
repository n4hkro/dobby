using Dobby.Core;
using Dobby.Host.Login;

namespace Dobby.Host;

internal sealed class DeferredLoginRuntimeWorkflow(Func<Task<PreparedLoginEntryRuntime?>> runtimeTaskProvider)
    : ILoginRuntimeWorkflow, ILoginCredentialWarmupWorkflow
{
    public LoginRuntimeWorkflowResult Submit(LoginSubmission submission)
    {
        try
        {
            var runtime = runtimeTaskProvider().GetAwaiter().GetResult();
            return runtime is null
                ? LoginRuntimeWorkflowResult.InfrastructureFailure(ErrorCode.DatabaseUnavailable)
                : runtime.PreLoginRuntime.LoginWorkflow.Submit(submission);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return LoginRuntimeWorkflowResult.InfrastructureFailure(ErrorCode.DatabaseUnavailable);
        }
    }

    public void WarmCredentials(LoginCredentialWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);

        _ = Task.Run(() =>
        {
            try
            {
                var runtime = runtimeTaskProvider().GetAwaiter().GetResult();
                if (runtime?.PreLoginRuntime.LoginWorkflow is ILoginCredentialWarmupWorkflow warmupWorkflow)
                {
                    warmupWorkflow.WarmCredentials(warmup);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
            }
        });
    }
}
