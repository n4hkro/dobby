namespace Dobby.Diagnostics;

public static class StartupMilestoneNames
{
    public const string HostBuilt = "startup.host-built";
    public const string ConfigLoaded = "startup.config-loaded";
    public const string PersistenceOpened = "startup.persistence-opened";
    public const string LoginVisible = "startup.login-visible";
    public const string PreAuthMetadataComplete = "startup.pre-auth-metadata-complete";
    public const string SessionAuthenticated = "startup.session-authenticated";
    public const string ShellFirstPaint = "startup.shell-first-paint";

    public static IReadOnlyList<string> All { get; } =
    [
        HostBuilt,
        ConfigLoaded,
        PersistenceOpened,
        LoginVisible,
        PreAuthMetadataComplete,
        SessionAuthenticated,
        ShellFirstPaint
    ];
}
