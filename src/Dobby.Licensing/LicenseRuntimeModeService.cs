namespace Dobby.Licensing;

public enum TrustedTimeHealth
{
    Healthy,
    RollbackDetected,
    Unavailable
}

public static class LicenseRuntimeModeService
{
    public static LicenseLifecycleState EvaluateLifecycle(
        LicensePayload payload,
        DateTimeOffset now,
        TrustedTimeHealth trustedTimeHealth = TrustedTimeHealth.Healthy,
        bool bindingValid = true)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return EvaluateLifecycle(
            payload.Intent,
            payload.NotBefore,
            payload.ExpiresAt,
            payload.GracePeriodDays,
            now,
            trustedTimeHealth,
            bindingValid);
    }

    public static LicenseLifecycleState EvaluateLifecycle(
        LicenseGrantIntent intent,
        DateTimeOffset notBefore,
        DateTimeOffset expiresAt,
        int gracePeriodDays,
        DateTimeOffset now,
        TrustedTimeHealth trustedTimeHealth = TrustedTimeHealth.Healthy,
        bool bindingValid = true)
    {
        if (trustedTimeHealth != TrustedTimeHealth.Healthy ||
            !bindingValid ||
            intent == LicenseGrantIntent.Revoked ||
            gracePeriodDays is < 0 or > LicenseV3Contract.MaximumGracePeriodDays ||
            notBefore > expiresAt)
        {
            return LicenseLifecycleState.Denied;
        }

        if (now < notBefore)
        {
            return LicenseLifecycleState.Pending;
        }

        if (now <= expiresAt)
        {
            return LicenseLifecycleState.Active;
        }

        try
        {
            return now <= expiresAt.AddDays(gracePeriodDays)
                ? LicenseLifecycleState.Grace
                : LicenseLifecycleState.ReadOnlyExpired;
        }
        catch (ArgumentOutOfRangeException)
        {
            return LicenseLifecycleState.Denied;
        }
    }

    public static LicenseLifecycleState EvaluateLifecycle(
        LicenseArtifactStatus status,
        DateTimeOffset notBefore,
        DateTimeOffset expiresAt,
        int gracePeriodDays,
        DateTimeOffset now)
    {
        return status switch
        {
            LicenseArtifactStatus.Denied => LicenseLifecycleState.Denied,
            LicenseArtifactStatus.ReadOnly => LicenseLifecycleState.ReadOnlyExpired,
            LicenseArtifactStatus.Pending => LicenseLifecycleState.Pending,
            LicenseArtifactStatus.Active => EvaluateLifecycle(
                LicenseGrantIntent.Granted,
                notBefore,
                expiresAt,
                gracePeriodDays,
                now),
            _ => LicenseLifecycleState.Denied
        };
    }
}
