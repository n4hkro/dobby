using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint7ReleaseEvidenceTests
{
    [Fact]
    public void Sprint7_evidence_records_shell_navigation_theme_localization_work_sessions_and_verification_closure()
    {
        var evidence = Sprint7Evidence();

        evidence.ShouldContain("## Sprint 7 Implementation Evidence");
        evidence.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        evidence.ShouldContain("AC-003");
        evidence.ShouldContain("AC-016");
        evidence.ShouldContain("AC-017");
        evidence.ShouldContain("AC-019");
        evidence.ShouldContain("AC-023");
        evidence.ShouldContain("AC-027");
        evidence.ShouldContain("AC-035");
        evidence.ShouldContain("AC-040");
        evidence.ShouldContain("AC-048");
        evidence.ShouldContain("AC-033 contribution");
        evidence.ShouldContain("rendered WPF evidence");
        evidence.ShouldContain("Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation");
        evidence.ShouldContain("rendered WPF child route tag `plugin.sample.analytics.daily`");
        evidence.ShouldContain("rendered WPF leaf route tag `plugin.sample.analytics.daily.filters`");
        evidence.ShouldContain("dobby.sprint7.toolchainPins.v1");
        evidence.ShouldContain("HD-S7-001");
        evidence.ShouldContain("Host.az-AZ.resx parity");
        evidence.ShouldContain("ShellLocalizationDiagnosticsRecorder");
        evidence.ShouldContain("Stay keeps the current route unchanged");
        evidence.ShouldContain("Minimize and Close continue requested navigation after resolving the pending work-session request");
        evidence.ShouldContain("Logout_button_uses_work_session_warning_and_cancel_or_close_before_returning_to_login");
        evidence.ShouldContain("Plugin_disable_request_uses_same_close_cancel_minimize_lifecycle_without_navigation");
        evidence.ShouldContain("WorkSessionShellIntegrationTests.Route_button_click_shows_warning_and_stay_minimize_close_resolve_navigation");
        evidence.ShouldContain("ShellViewPreference fields are adapted through Host into ShellLayoutPreference runtime state");
        evidence.ShouldContain("ShellPreferenceMapperTests.Shell_runtime_preference_adapter_round_trips_all_committed_sprint7_fields");
        evidence.ShouldContain("SettingsPreferencePersistenceTests.Preferences_persist_through_host_configuration_table_without_new_schema");
        evidence.ShouldContain("1366x768 automated pass");
        evidence.ShouldContain("1920x1080 125% DPI automated pass");
        evidence.ShouldContain("2560x1440 automated pass");
        evidence.ShouldContain("3840x2160 automated pass");
        evidence.ShouldContain("150% DPI automated pass");
        evidence.ShouldContain("Toast/Banner/Modal rendered presentation");
        evidence.ShouldContain("Loading/Empty/Success/Error/Confirmation rendered presentation");
        evidence.ShouldContain("Rendered_shell_supports_keyboard_traversal_focusable_actions_and_accessible_labels");
        evidence.ShouldContain("Sprint 7 local automation substitution for manual UI evidence");
        evidence.ShouldContain("accepted substitute for the manual/out-of-band resolution, DPI, theme-switch, accessibility, shell-state, and Settings/About rows");
        evidence.ShouldContain("ProfileMenuButton");
        evidence.ShouldContain("ProfileMenuPanel");
        evidence.ShouldContain("Theme_contrast_tokens_meet_minimum_accessibility_ratio");
        evidence.ShouldContain("minimum contrast");
        evidence.ShouldContain("ShellBackgroundOperationCoordinator");
        evidence.ShouldContain("No Dobby.Shell -> Dobby.Configuration reference");
        evidence.ShouldContain("No plugin activation/package validation");
        evidence.ShouldContain("No telemetry/vendor upload");
        evidence.ShouldContain("No schema widening");
        evidence.ShouldContain("No new Core ErrorCode");
        evidence.ShouldContain("No new audit event name");
        evidence.ShouldNotContain("Unsupported manual resolution/DPI matrix coverage is not claimed");
    }

    private static string Sprint7Evidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 7 Implementation Evidence", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextSection = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        return nextSection >= 0
            ? text[start..nextSection]
            : text[start..];
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
