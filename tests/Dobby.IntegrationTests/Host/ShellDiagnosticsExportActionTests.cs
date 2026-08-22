using System.IO.Compression;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Diagnostics;
using Dobby.Host;
using Dobby.Host.Plugins;
using Dobby.IntegrationTests.Shell;
using Dobby.Localization;
using Dobby.PluginFramework.Packaging;
using Dobby.Shell;
using Shouldly;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dobby.IntegrationTests.Host;

[Collection(WpfIntegrationTestCollection.Name)]
public sealed class ShellDiagnosticsExportActionTests
{
    [Fact]
    public void Authenticated_shell_action_builds_local_redacted_export_and_audits_existing_event()
    {
        using var fixture = DiagnosticsExportFixture.Create();
        var audit = new InMemoryAuditTrail();
        var coordinator = new ShellDiagnosticsExportCoordinator(
            new DiagnosticsExportPackageBuilder(),
            audit,
            TimeProvider.System);
        var correlationId = CorrelationId.New();

        var result = coordinator.Export(
            "owner-1",
            fixture.SourceDirectory,
            fixture.OutputZipPath,
            correlationId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Uploaded.ShouldBeFalse();
        File.Exists(fixture.OutputZipPath).ShouldBeTrue();
        using (var archive = ZipFile.OpenRead(fixture.OutputZipPath))
        {
            archive.Entries.Select(entry => entry.FullName).ShouldContain("diagnostics.log");
            archive.Entries.Select(entry => entry.FullName).ShouldNotContain("passwords.txt");
            using var entry = archive.GetEntry("diagnostics.log")!.Open();
            using var reader = new StreamReader(entry);
            var redacted = reader.ReadToEnd();
            redacted.ShouldContain("<redacted>", Case.Sensitive);
            redacted.ShouldNotContain("abc", Case.Sensitive);
        }

        var auditEvent = audit.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.DiagnosticsExportCreated);
        auditEvent.ActorId.ShouldBe("owner-1");
        auditEvent.CorrelationId.ShouldBe(correlationId);
        auditEvent.Details!["uploaded"].ShouldBe("false");
    }

    [Fact]
    public void Readonly_expired_diagnostics_export_denies_before_creating_zip_and_audits_denial()
    {
        using var fixture = DiagnosticsExportFixture.Create();
        var audit = new InMemoryAuditTrail();
        var correlationId = CorrelationId.New();
        var coordinator = new ShellDiagnosticsExportCoordinator(
            new DiagnosticsExportPackageBuilder(),
            audit,
            TimeProvider.System,
            outputGate: () => Result.Failure(ErrorCode.LicenseReadOnly));

        var result = coordinator.Export(
            "owner-1",
            fixture.SourceDirectory,
            fixture.OutputZipPath,
            correlationId);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.LicenseReadOnly);
        File.Exists(fixture.OutputZipPath).ShouldBeFalse();
        var auditEvent = audit.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.DiagnosticsExportCreated);
        auditEvent.ActorId.ShouldBe("owner-1");
        auditEvent.Result.ShouldBe(AuditResult.Denied);
        auditEvent.CorrelationId.ShouldBe(correlationId);
        auditEvent.Details!["uploaded"].ShouldBe("false");
    }

    [Fact]
    public void Authenticated_shell_diagnostics_export_button_creates_package()
    {
        using var fixture = DiagnosticsExportFixture.Create();
        var calls = 0;

        RunOnSta(() =>
        {
            var window = new MainWindow(
                ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked()),
                actionOverrides: new ShellActionOverrides
                {
                    ExportDiagnostics = correlationId =>
                    {
                        calls++;
                        var coordinator = new ShellDiagnosticsExportCoordinator(
                            new DiagnosticsExportPackageBuilder(),
                            new InMemoryAuditTrail(),
                            TimeProvider.System);
                        return coordinator.Export("owner-1", fixture.SourceDirectory, fixture.OutputZipPath, correlationId);
                    }
                })
            {
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                FindNamed<Button>(window, "ProfileMenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                FindNamed<Button>(window, "DiagnosticsExportButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                calls.ShouldBe(1);
                File.Exists(fixture.OutputZipPath).ShouldBeTrue();
                ((ShellViewModel)window.DataContext).StatusText.ShouldBe("Diaqnostika ixrac edildi");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Authenticated_shell_diagnostics_export_maps_license_readonly_to_denied_feedback()
    {
        var strings = HostStringCatalog.CreateResourceBacked();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    ExportDiagnostics = _ => Result.Failure<DiagnosticsExportPackage>(ErrorCode.LicenseReadOnly)
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);
                FindNamed<Button>(window, "DiagnosticsExportButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellDiagnosticsExportDenied));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Plugins_manager_install_button_calls_backend_and_maps_readonly_denial_to_policy_feedback()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var calls = 0;

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            shell.SetPluginsManagerSurface(canView: true, canImport: true, canConfigure: false, canDelete: false, []);
            shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    InstallPluginPackageAsync = (_, _) =>
                    {
                        calls++;
                        shell.PluginsManagerFeedbackText.ShouldBe(strings.GetString(HostResourceKeys.PluginsManagerInstallBusy));
                        return Task.FromResult(Result.Failure<InstalledPluginState>(ErrorCode.LicenseReadOnly));
                    }
                });

            try
            {
                ShowWindow(window);
                FindNamed<Button>(window, "PluginsManagerInstallButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                calls.ShouldBe(1);
                shell.PluginsManagerFeedbackText.ShouldBe(strings.GetString(HostResourceKeys.PluginsManagerOperationDenied));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Plugins_manager_lifecycle_buttons_call_backend_and_uninstall_uses_explicit_data_choice()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var calls = new List<string>();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            shell.SetPluginsManagerSurface(
                canView: true,
                canImport: false,
                canConfigure: true,
                canDelete: true,
                [
                    new ShellPluginManagerItem(
                        "com.dobby.sample",
                        "Sample plugin",
                        "1.0.0",
                        "Verification plugin",
                        IsEnabled: true,
                        "Enabled",
                        IsPendingUninstall: false,
                        RestartRequired: false)
                ]);
            shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    DisablePluginAsync = (pluginId, _, _) =>
                    {
                        calls.Add($"disable:{pluginId}");
                        return Task.FromResult(Result.Success());
                    },
                    EnablePluginAsync = (pluginId, _, _) =>
                    {
                        calls.Add($"enable:{pluginId}");
                        return Task.FromResult(Result.Success());
                    },
                    ConfirmPluginUninstall = request =>
                    {
                        calls.Add($"confirm:{request.PluginId}:{request.WasActivatedInCurrentProcess}");
                        return PluginUninstallConfirmationResult.Confirm(PluginUninstallDataChoice.DeleteData);
                    },
                    UninstallPluginAsync = (pluginId, dataChoice, _, _) =>
                    {
                        calls.Add($"uninstall:{pluginId}:{dataChoice}");
                        return Task.FromResult(Result.Success());
                    }
                });

            try
            {
                ShowWindow(window);
                FindNamed<Button>(window, "PluginsManagerDisableButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                shell.SetPluginsManagerSurface(
                    canView: true,
                    canImport: false,
                    canConfigure: true,
                    canDelete: true,
                    [
                        new ShellPluginManagerItem(
                            "com.dobby.sample",
                            "Sample plugin",
                            "1.0.0",
                            "Verification plugin",
                            IsEnabled: false,
                            "Disabled",
                            IsPendingUninstall: false,
                            RestartRequired: false)
                    ]);
                FindNamed<Button>(window, "PluginsManagerEnableButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                FindNamed<Button>(window, "PluginsManagerUninstallButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                calls.ShouldBe([
                    "disable:com.dobby.sample",
                    "enable:com.dobby.sample",
                    "confirm:com.dobby.sample:False",
                    "uninstall:com.dobby.sample:DeleteData"
                ]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Authenticated_shell_change_password_button_calls_backend_and_shows_busy_then_success_feedback()
    {
        var calls = 0;
        string? busyTextSeenByBackend = null;
        var strings = HostStringCatalog.CreateResourceBacked();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    ChangePassword = _ =>
                    {
                        calls++;
                        busyTextSeenByBackend = shell.StatusText;
                        return Result.Success();
                    }
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);
                FindNamed<PasswordBox>(window, "CurrentPasswordBox").Password = "current password";
                FindNamed<PasswordBox>(window, "NewPasswordBox").Password = "new password";
                FindNamed<PasswordBox>(window, "ConfirmNewPasswordBox").Password = "new password";
                FindNamed<Button>(window, "ChangePasswordButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                calls.ShouldBe(1);
                busyTextSeenByBackend.ShouldBe(strings.GetString(HostResourceKeys.ShellProfilePasswordChangeBusy));
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfilePasswordChangeSuccess));
                FindNamed<PasswordBox>(window, "CurrentPasswordBox").Password.ShouldBeEmpty();
                FindNamed<PasswordBox>(window, "NewPasswordBox").Password.ShouldBeEmpty();
                FindNamed<PasswordBox>(window, "ConfirmNewPasswordBox").Password.ShouldBeEmpty();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Authenticated_shell_rejects_password_confirmation_mismatch_before_backend_call()
    {
        var calls = 0;
        var strings = HostStringCatalog.CreateResourceBacked();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    ChangePassword = _ =>
                    {
                        calls++;
                        return Result.Success();
                    }
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);
                FindNamed<PasswordBox>(window, "CurrentPasswordBox").Password = "current password";
                FindNamed<PasswordBox>(window, "NewPasswordBox").Password = "new password";
                FindNamed<PasswordBox>(window, "ConfirmNewPasswordBox").Password = "different password";

                FindNamed<Button>(window, "ChangePasswordButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                calls.ShouldBe(0);
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfilePasswordChangeError));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Plugin_install_operation_keeps_dispatcher_responsive_until_explicit_completion()
    {
        var strings = HostStringCatalog.CreateResourceBacked();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource<Result<InstalledPluginState>>(TaskCreationOptions.RunContinuationsAsynchronously);

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            shell.SetPluginsManagerSurface(canView: true, canImport: true, canConfigure: false, canDelete: false, []);
            shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    InstallPluginPackageAsync = async (_, cancellationToken) =>
                    {
                        started.TrySetResult();
                        return await finish.Task.WaitAsync(cancellationToken);
                    }
                });

            try
            {
                ShowWindow(window);
                FindNamed<Button>(window, "PluginsManagerInstallButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(window.Dispatcher, () => started.Task.IsCompleted);

                var heartbeatRan = false;
                _ = window.Dispatcher.InvokeAsync(
                    () => heartbeatRan = true,
                    DispatcherPriority.Input);
                PumpUntil(window.Dispatcher, () => heartbeatRan);

                shell.PluginsManagerFeedbackText.ShouldBe(strings.GetString(HostResourceKeys.PluginsManagerInstallBusy));
                shell.PluginsManagerInstallIsEnabled.ShouldBeFalse();

                finish.TrySetResult(Result.Failure<InstalledPluginState>(ErrorCode.Cancelled));
                PumpUntil(window.Dispatcher, () => shell.PluginsManagerInstallIsEnabled);
                shell.PluginsManagerFeedbackText.ShouldBeEmpty();
            }
            finally
            {
                finish.TrySetResult(Result.Failure<InstalledPluginState>(ErrorCode.Cancelled));
                window.Close();
                PumpUntil(window.Dispatcher, () => shell.PluginsManagerInstallIsEnabled);
            }
        });
    }

    [Fact]
    public void Closing_shell_cancels_inflight_plugin_install_without_late_busy_state()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backendCompleted = new TaskCompletionSource<Result<InstalledPluginState>>();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
            shell.SetPluginsManagerSurface(canView: true, canImport: true, canConfigure: false, canDelete: false, []);
            shell.ActivateRoute(Dobby.Navigation.RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    InstallPluginPackageAsync = (_, cancellationToken) =>
                    {
                        started.TrySetResult();
                        cancellationToken.Register(() =>
                        {
                            cancellationObserved.TrySetResult();
                            backendCompleted.TrySetResult(
                                Result.Failure<InstalledPluginState>(ErrorCode.Cancelled));
                        });
                        return backendCompleted.Task;
                    }
                });

            ShowWindow(window);
            FindNamed<Button>(window, "PluginsManagerInstallButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(window.Dispatcher, () => started.Task.IsCompleted);

            window.Close();
            PumpUntil(
                window.Dispatcher,
                () => cancellationObserved.Task.IsCompleted && shell.PluginsManagerInstallIsEnabled);

            cancellationObserved.Task.IsCompletedSuccessfully.ShouldBeTrue();
            shell.PluginsManagerInstallIsEnabled.ShouldBeTrue();
        });
    }

    [Fact]
    public void Authenticated_shell_manual_lock_button_calls_backend_and_shows_busy_then_success_feedback()
    {
        var calls = 0;
        string? busyTextSeenByBackend = null;
        var strings = HostStringCatalog.CreateResourceBacked();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    ManualLock = _ =>
                    {
                        calls++;
                        busyTextSeenByBackend = shell.StatusText;
                        return Result.Success();
                    }
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);
                FindNamed<Button>(window, "ManualLockButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                calls.ShouldBe(1);
                busyTextSeenByBackend.ShouldBe(strings.GetString(HostResourceKeys.ShellProfileManualLockBusy));
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfileManualLockSuccess));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Authenticated_shell_manual_lock_logout_result_closes_shell_and_requests_login()
    {
        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    ManualLock = _ => Result.Failure(ErrorCode.Cancelled)
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);
                FindNamed<Button>(window, "ManualLockButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                window.ReturnToLoginRequested.ShouldBeTrue();
                window.IsLoaded.ShouldBeFalse();
            }
            finally
            {
                if (window.IsLoaded)
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void Authenticated_shell_logout_button_calls_backend_and_requests_return_to_login()
    {
        var calls = 0;
        string? busyTextSeenByBackend = null;
        var strings = HostStringCatalog.CreateResourceBacked();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    Logout = _ =>
                    {
                        calls++;
                        busyTextSeenByBackend = shell.StatusText;
                        return Result.Success();
                    }
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);
                FindNamed<Button>(window, "LogoutButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                calls.ShouldBe(1);
                busyTextSeenByBackend.ShouldBe(strings.GetString(HostResourceKeys.ShellProfileLogoutBusy));
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfileLogoutSuccess));
                window.ReturnToLoginRequested.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Authenticated_shell_profile_actions_map_forbidden_results_to_denied_feedback()
    {
        var strings = HostStringCatalog.CreateResourceBacked();

        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(strings);
            var window = CreateHiddenWindow(
                shell,
                new ShellActionOverrides
                {
                    ChangePassword = _ => Result.Failure(ErrorCode.Forbidden),
                    ManualLock = _ => Result.Failure(ErrorCode.Forbidden),
                    Logout = _ => Result.Failure(ErrorCode.Forbidden)
                });

            try
            {
                ShowWindow(window);
                OpenProfileMenu(window);

                FindNamed<PasswordBox>(window, "CurrentPasswordBox").Password = "current password";
                FindNamed<PasswordBox>(window, "NewPasswordBox").Password = "new password";
                FindNamed<PasswordBox>(window, "ConfirmNewPasswordBox").Password = "new password";
                FindNamed<Button>(window, "ChangePasswordButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfilePasswordChangeDenied));

                FindNamed<Button>(window, "ManualLockButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfileManualLockDenied));

                FindNamed<Button>(window, "LogoutButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                shell.StatusText.ShouldBe(strings.GetString(HostResourceKeys.ShellProfileLogoutDenied));
                window.ReturnToLoginRequested.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class DiagnosticsExportFixture : IDisposable
    {
        private DiagnosticsExportFixture(string root)
        {
            Root = root;
            SourceDirectory = Path.Combine(root, "source");
            Directory.CreateDirectory(SourceDirectory);
            File.WriteAllText(Path.Combine(SourceDirectory, "diagnostics.log"), "token=abc");
            File.WriteAllText(Path.Combine(SourceDirectory, "passwords.txt"), "secret");
            OutputZipPath = Path.Combine(root, "dobby-diagnostics.zip");
        }

        public string Root { get; }

        public string SourceDirectory { get; }

        public string OutputZipPath { get; }

        public static DiagnosticsExportFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-shell-diagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new DiagnosticsExportFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static void AddThemeResources(Window window)
    {
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Dobby.UI;component/Themes/DesignTokens.xaml", UriKind.Relative)
        });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/Dobby.UI;component/Themes/LightTheme.xaml", UriKind.Relative)
        });
    }

    private static MainWindow CreateHiddenWindow(ShellViewModel shell, ShellActionOverrides actionOverrides)
    {
        return new MainWindow(shell, actionOverrides: actionOverrides)
        {
            Left = -32000,
            Top = -32000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize
        };
    }

    private static void ShowWindow(Window window)
    {
        AddThemeResources(window);
        window.Show();
        Pump(window);
    }

    private static void OpenProfileMenu(Window window)
    {
        FindNamed<Button>(window, "ProfileMenuButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump(window);
    }

    private static T FindNamed<T>(Window window, string name)
        where T : FrameworkElement
    {
        return FindVisuals<T>((DependencyObject)window.Content)
            .Single(element => element.Name.Equals(name, StringComparison.Ordinal));
    }

    private static IReadOnlyList<T> FindVisuals<T>(DependencyObject root)
        where T : DependencyObject
    {
        var matches = new List<T>();
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                matches.Add(match);
            }

            matches.AddRange(FindVisuals<T>(child));
        }

        return matches;
    }

    private static void Pump(Window window)
    {
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static void PumpUntil(Dispatcher dispatcher, Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The explicit dispatcher completion signal was not observed.");
            }

            dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
