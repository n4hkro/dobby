namespace Dobby.Audit;

public static class AuditResultExtensions
{
    public static string ToPayloadValue(this AuditResult result)
    {
        return result switch
        {
            AuditResult.Success => "success",
            AuditResult.Failed => "failed",
            AuditResult.Denied => "denied",
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported audit result.")
        };
    }
}
