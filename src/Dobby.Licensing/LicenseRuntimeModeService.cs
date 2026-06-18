namespace Dobby.Licensing;

public static class LicenseRuntimeModeService
{
    public static LicenseLifecycleState EvaluateLifecycle(LicensePayload payload, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return EvaluateLifecycle(
            payload.Status,
            payload.NotBefore,
            payload.ExpiresAt,
            payload.GracePeriodDays,
            now);
    }

    public static LicenseLifecycleState EvaluateLifecycle(
        LicenseArtifactStatus status,
        DateTimeOffset notBefore,
        DateTimeOffset expiresAt,
        int gracePeriodDays,
        DateTimeOffset now)
    {
        if (status == LicenseArtifactStatus.Denied)
        {
            return LicenseLifecycleState.Denied;
        }

        if (status == LicenseArtifactStatus.ReadOnly)
        {
            return LicenseLifecycleState.ReadOnly;
        }

        if (now < notBefore)
        {
            return LicenseLifecycleState.Pending;
        }

        if (now <= expiresAt)
        {
            return LicenseLifecycleState.Active;
        }

        return now <= expiresAt.AddDays(gracePeriodDays)
            ? LicenseLifecycleState.Grace
            : LicenseLifecycleState.ReadOnly;
    }
}
