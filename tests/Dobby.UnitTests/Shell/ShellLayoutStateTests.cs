using Dobby.Shell.Layout;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Localization;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class ShellLayoutStateTests
{
    [Fact]
    public void Default_layout_state_exposes_required_sprint7_shell_surfaces()
    {
        var state = ShellLayoutState.CreateDefault();

        state.SurfaceNames.ShouldBe([
            "TopToolbar",
            "Sidebar",
            "ThirdBar",
            "MainContent",
            "BottomStatus",
            "MinimizedWorkStrip"
        ]);
    }

    [Fact]
    public void Default_layout_state_exposes_profile_menu_notification_and_feedback_taxonomies()
    {
        var state = ShellLayoutState.CreateDefault();

        state.ProfileMenuItems.Select(item => item.Key).ShouldBe([
            "profile-account-info",
            "change-password",
            "manual-lock",
            "logout"
        ]);
        state.NotificationKinds.ShouldBe([
            ShellNotificationKind.Toast,
            ShellNotificationKind.Banner,
            ShellNotificationKind.Modal
        ]);
        state.FeedbackStates.ShouldBe([
            ShellFeedbackState.Loading,
            ShellFeedbackState.Empty,
            ShellFeedbackState.Success,
            ShellFeedbackState.Error,
            ShellFeedbackState.Confirmation
        ]);
    }

    [Fact]
    public void View_model_localizes_notification_and_feedback_presentations()
    {
        var shell = Dobby.Shell.ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());

        shell.NotificationPresentation.Select(item => item.Label).ShouldBe([
            "Qısa bildiriş",
            "Banner bildirişi",
            "Modal bildiriş"
        ]);
        shell.FeedbackStatePresentation.Select(item => item.Label).ShouldBe([
            "Yüklənir",
            "Məlumat yoxdur",
            "Uğurlu",
            "Xəta",
            "Təsdiq"
        ]);
    }

    [Fact]
    public void View_model_state_can_carry_rendered_settings_about_and_background_status()
    {
        var state = ShellLayoutState.CreateDefault()
            .WithSettingsAndAbout(
                [
                    new ShellSettingsCategoryItem("appearance", "Görünüş", IsAvailable: true),
                    new ShellSettingsCategoryItem("about", "Haqqında", IsAvailable: true)
                ],
                [
                    new ShellAboutField("app-version", "Versiya", "0.1.0"),
                    new ShellAboutField("plugins", "Pluginlər", "Quraşdırılmış plugin yoxdur")
                ])
            .WithBackgroundOperation(new BackgroundOperationSnapshot(
                Guid.NewGuid(),
                BackgroundOperationOwnerKind.Host,
                OwnerId: null,
                "Dobby.Host.AuditRetentionSweep",
                ShortDetailKey: null,
                BackgroundOperationStatus.Running,
                new BackgroundOperationProgress(Percent: 42, IsIndeterminate: false),
                CanCancel: false,
                Guid.NewGuid(),
                FailureReason: null));

        state.SettingsCategories.Select(category => category.Key).ShouldBe(["appearance", "about"]);
        state.AboutFields.Select(field => field.Key).ShouldBe(["app-version", "plugins"]);
        state.BackgroundOperationText.ShouldBe("Dobby.Host.AuditRetentionSweep");
        state.BackgroundOperationPercent.ShouldBe(42);
        state.BackgroundOperationIsIndeterminate.ShouldBeFalse();
    }
}
