using System.Text.RegularExpressions;

namespace Dobby.Audit;

internal static partial class AuditDetailsSanitizer
{
    private const int MaximumDetailCount = 16;
    private const int MaximumDetailValueLength = 256;

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedFieldsByEvent =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AuditEventName.UserCreate] = Fields("bootstrapKind", "principalRole", "permissionState", "composition"),
            [AuditEventName.UserLoginFailed] = Fields("reason"),
            [AuditEventName.SessionUnlock] = Fields("reason"),
            [AuditEventName.SessionExpired] = Fields("reason"),
            [AuditEventName.SessionInvalidated] = Fields("reason"),
            [AuditEventName.UserPasswordChange] = Fields("state", "reason"),
            [AuditEventName.UserPasswordReset] = Fields("reason"),
            [AuditEventName.PermissionGrant] = Fields("permissionKey"),
            [AuditEventName.PermissionRevoke] = Fields("permissionKey"),
            [AuditEventName.PluginInstallAttempted] = Fields("diagnosticMarker"),
            [AuditEventName.PluginInstallVerified] = Fields("pluginId", "fingerprint", "diagnosticMarker"),
            [AuditEventName.PluginInstallRejected] = Fields("pluginId", "reasonCode", "diagnosticMarker"),
            [AuditEventName.PluginInstallSignatureFailed] = Fields("pluginId", "reasonCode", "diagnosticMarker"),
            [AuditEventName.PluginInstallTrustFailed] = Fields("pluginId", "reasonCode", "diagnosticMarker"),
            [AuditEventName.PluginEnable] = Fields("pluginId", "status", "diagnosticMarker"),
            [AuditEventName.PluginDisable] = Fields("pluginId", "status", "diagnosticMarker"),
            [AuditEventName.PluginUninstall] = Fields("pluginId", "dataChoice", "restartRequired", "diagnosticMarker"),
            [AuditEventName.PluginActivationSkipped] = Fields("pluginId", "status", "reasonCode", "diagnosticMarker"),
            [AuditEventName.PluginActivationRejected] = Fields("pluginId", "status", "reasonCode", "diagnosticMarker"),
            [AuditEventName.PluginRuntimeFaulted] = Fields("pluginId", "status", "reasonCode", "diagnosticMarker"),
            [AuditEventName.PluginRuntimeQuarantined] = Fields("pluginId", "status", "reasonCode", "defectCount", "diagnosticMarker"),
            [AuditEventName.PluginLifecycleV1AdapterUsed] = Fields("pluginId", "status", "reasonCode", "diagnosticMarker"),
            [AuditEventName.RecoveryExport] = Fields("requestId", "nonceHash"),
            [AuditEventName.RecoveryImport] = Fields("outcome", "requestId", "nonceHash"),
            [AuditEventName.LicenseImportAttempted] = Fields("reasonCode", "diagnosticMarker"),
            [AuditEventName.LicenseImportSucceeded] = Fields("reasonCode", "diagnosticMarker", "lifecycleState"),
            [AuditEventName.LicenseImportRejected] = Fields("reasonCode", "diagnosticMarker"),
            [AuditEventName.LicenseEnforcementDenied] = Fields("reasonCode"),
            [AuditEventName.LicenseEnforcementWarning] = Fields("reasonCode", "lifecycleState"),
            [AuditEventName.CustomerDeploymentContextProvision] = Fields("customerIdHash", "deploymentIdHash", "supportCode"),
            [AuditEventName.DiagnosticsExportCreated] = Fields("outputName", "included", "excluded", "uploaded"),
            [AuditEventName.SecurityVerificationFailed] = Fields("reason", "outcome", "requestId"),
            [AuditEventName.PersistenceSnapshotCreated] = Fields("outputName", "sha256", "reasonCode"),
            [AuditEventName.PersistenceRestoreRequested] = Fields("candidateName", "reasonCode"),
            [AuditEventName.PersistenceRestoreCompleted] = Fields("candidateName", "quarantineName", "reasonCode")
        };

    private static readonly string[] ForbiddenKeyFragments =
    [
        "password",
        "token",
        "privateKey",
        "signature",
        "session",
        "remember",
        "recoverySecret",
        "recoveryNonce",
        "recoveryResponse",
        "licensePayload",
        "licenseArtifact",
        "licenseBinding",
        "deviceFingerprint",
        "hardware"
    ];

    public static AuditEvent Sanitize(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        EnsureEnvelopeText(auditEvent.EventName, 128, requireEventToken: true);
        EnsureEnvelopeText(auditEvent.ActorId, 160, requireEventToken: false);
        EnsureEnvelopeText(auditEvent.Target, 256, requireEventToken: false);
        return auditEvent with
        {
            Details = Sanitize(auditEvent.EventName, auditEvent.Details)
        };
    }

    public static IReadOnlyDictionary<string, string>? Sanitize(
        string eventName,
        IReadOnlyDictionary<string, string>? details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (details is null)
        {
            return null;
        }

        if (details.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (details.Count > MaximumDetailCount ||
            !AllowedFieldsByEvent.TryGetValue(eventName, out var allowedFields))
        {
            throw new InvalidOperationException("Audit details are not approved for this event.");
        }

        var sanitized = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var detail in details)
        {
            EnsureSafe(detail.Key, detail.Value, allowedFields);
            sanitized.Add(detail.Key, detail.Value);
        }

        return sanitized;
    }

    private static void EnsureSafe(
        string key,
        string? value,
        IReadOnlySet<string> allowedFields)
    {
        if (!allowedFields.Contains(key) ||
            !FieldNamePattern().IsMatch(key) ||
            ForbiddenKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Audit detail field is not approved for this event.");
        }

        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumDetailValueLength ||
            value.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029') ||
            SensitiveAssignmentPattern().IsMatch(value) ||
            PemPattern().IsMatch(value) ||
            WindowsPathPattern().IsMatch(value) ||
            UncPathPattern().IsMatch(value) ||
            SqlPattern().IsMatch(value))
        {
            throw new InvalidOperationException("Audit detail value is malformed or contains unapproved diagnostic data.");
        }
    }

    private static IReadOnlySet<string> Fields(params string[] names) =>
        new HashSet<string>(names, StringComparer.Ordinal);

    private static void EnsureEnvelopeText(string value, int maximumLength, bool requireEventToken)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
            value.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029') ||
            (requireEventToken && !EventNamePattern().IsMatch(value)) ||
            PemPattern().IsMatch(value) ||
            WindowsPathPattern().IsMatch(value) ||
            UncPathPattern().IsMatch(value))
        {
            throw new InvalidOperationException("Audit event envelope contains unapproved data.");
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldNamePattern();

    [GeneratedRegex("^[a-z][a-z0-9.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex EventNamePattern();

    [GeneratedRegex("(?<key>password|passwordHash|token|privateKey|signature|session|sessionMaterial|rememberMe|rememberMeSecret|recoverySecret|recoveryNonce|recoveryResponse|licensePayload|licenseArtifact|licenseBinding|deviceFingerprint|connectionString)\\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern();

    [GeneratedRegex("-----BEGIN(?: [A-Z0-9]+)* PRIVATE KEY-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PemPattern();

    [GeneratedRegex(@"[A-Za-z]:[\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathPattern();

    [GeneratedRegex(@"\\\\[^\\\s]+[\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex UncPathPattern();

    [GeneratedRegex(@"\b(?:SELECT|INSERT|UPDATE|DELETE|ALTER|CREATE|DROP|PRAGMA)\b.+\b(?:FROM|INTO|SET|TABLE|INDEX)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqlPattern();
}
