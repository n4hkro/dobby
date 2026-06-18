namespace Dobby.Licensing;

public static class LicenseRuntimeModeService
{
    public static LicenseLifecycleState EvaluateLifecycle(LicensePayload payload, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Status == LicenseArtifactStatus.Denied)
        {
            return LicenseLifecycleState.Denied;
        }

        if (payload.Status == LicenseArtifactStatus.ReadOnly)
        {
            return LicenseLifecycleState.ReadOnly;
        }

        if (now < payload.NotBefore)
        {
            return LicenseLifecycleState.Pending;
        }

        if (now <= payload.ExpiresAt)
        {
            return LicenseLifecycleState.Active;
        }

        return now <= payload.ExpiresAt.AddDays(payload.GracePeriodDays)
            ? LicenseLifecycleState.Grace
            : LicenseLifecycleState.ReadOnly;
    }
}
