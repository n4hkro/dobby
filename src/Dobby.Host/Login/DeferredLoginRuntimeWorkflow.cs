using Dobby.Core;
using Dobby.Host.Login;

namespace Dobby.Host;

internal sealed class DeferredLoginRuntimeWorkflow(Func<Task<PreparedLoginEntryRuntime?>> runtimeTaskProvider)
    : ILoginRuntimeWorkflow
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
}
