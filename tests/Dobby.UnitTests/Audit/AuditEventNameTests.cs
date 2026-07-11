using Dobby.Audit;
using Shouldly;

namespace Dobby.UnitTests.Audit;

public sealed class AuditEventNameTests
{
    [Fact]
    public void Canonical_names_match_audit_spec_taxonomy()
    {
        AuditEventName.All.ShouldBe(
        [
            "user.login",
            "user.login.failed",
            "user.logout",
            "session.lock",
            "session.unlock",
            "session.expired",
            "session.invalidated",
            "user.create",
            "user.update",
            "user.password.change",
            "user.password.reset",
            "user.activate",
            "user.deactivate",
            "permission.grant",
            "permission.revoke",
            "permission.denied",
            "owner.protection.denied",
            "plugin.install.attempted",
            "plugin.install.verified",
            "plugin.install.rejected",
            "plugin.install.signature-failed",
            "plugin.install.trust-failed",
            "plugin.enable",
            "plugin.disable",
            "plugin.uninstall",
            "plugin.activation.skipped",
            "plugin.activation.rejected",
            "plugin.runtime.faulted",
            "recovery.export",
            "recovery.import",
            "license.import.attempted",
            "license.import.succeeded",
            "license.import.rejected",
            "license.enforcement.denied",
            "license.enforcement.warning",
            "customer-deployment-context.provision",
            "diagnostics.export.created",
            "security.verification.failed"
        ]);
    }
}
