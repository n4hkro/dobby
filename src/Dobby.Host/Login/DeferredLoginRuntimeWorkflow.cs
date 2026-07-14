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

    public Task WarmCredentialsAsync(LoginCredentialWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);

        return Task.Run(async () =>
        {
            try
            {
                var runtime = await runtimeTaskProvider().ConfigureAwait(false);
                if (runtime?.PreLoginRuntime.LoginWorkflow is ILoginCredentialWarmupWorkflow warmupWorkflow)
                {
                    await warmupWorkflow.WarmCredentialsAsync(warmup).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
            }
        });
    }
}
