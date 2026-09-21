using Dobby.Core;
using Dobby.PluginFramework.Activation;

namespace Dobby.Host.Plugins;

/// <summary>
/// Single emission point for the <c>plugin.runtime.quarantined</c> audit event.
/// </summary>
/// <remarks>
/// Process-local quarantine permanently stops a plugin for the remainder of the process
/// lifetime: routes, subscriptions, services and work sessions are torn down. That is a
/// lifecycle/security-significant transition, so it must leave audit evidence rather than a
/// diagnostics-only marker (<c>docs/specs/plugins-manager.md</c> § audit,
/// <c>docs/specs/audit-and-diagnostics.md</c> taxonomy, ADR-0059).
///
/// Three independent runtime surfaces observe the fault policy — command dispatch, page route
/// activation and plugin event-handler outcomes — and each performs its own cleanup. The audit
/// emission is centralized here so a future fourth observer cannot silently reintroduce a
/// quarantine path with no audit trail.
/// </remarks>
internal static class PluginQuarantineAuditEmitter
{
    /// <summary>
    /// Emits the quarantine audit event for a decision whose <see cref="PluginRuntimeFaultDecision.QuarantineStarted"/>
    /// edge has just been crossed. No-ops when the decision is not a quarantine start, so callers
    /// may invoke it unconditionally after <c>Observe</c>.
    /// </summary>
    public static void EmitIfQuarantineStarted(
        PluginRuntimeAuditCoordinator? auditCoordinator,
        PluginRuntimeFaultDecision decision,
        string pluginId,
        PluginInvocationOutcomeKind outcomeKind,
        string? actorId,
        CorrelationId? correlationId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.QuarantineStarted || auditCoordinator is null)
        {
            return;
        }

        auditCoordinator.AppendPluginQuarantined(
            string.IsNullOrWhiteSpace(actorId) ? "system" : actorId,
            pluginId,
            ReasonCode(outcomeKind),
            decision.State.DefectCount,
            correlationId ?? CorrelationId.New());
    }

    private static string ReasonCode(PluginInvocationOutcomeKind outcomeKind) => outcomeKind switch
    {
        PluginInvocationOutcomeKind.TimedOut => "runtime-timeout-threshold",
        PluginInvocationOutcomeKind.PluginDeclaredFailure => "runtime-declared-failure-threshold",
        PluginInvocationOutcomeKind.PluginException => "runtime-exception-threshold",
        PluginInvocationOutcomeKind.ContractBreach => "runtime-contract-breach-threshold",
        _ => "runtime-defect-threshold"
    };
}
