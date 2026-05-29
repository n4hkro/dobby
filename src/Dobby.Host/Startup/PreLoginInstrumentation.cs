namespace Dobby.Host.Startup;

public sealed class PreLoginInstrumentation
{
    public static IReadOnlyList<string> ForbiddenWorkloadNames { get; } =
    [
        "plugin-discovery",
        "plugin-activation",
        "plugin-runtime-binding",
        "plugin-signature-verification",
        "online-check",
        "update-check",
        "database-migration",
        "heavy-migration",
        "import-export",
        "license-heavy-verification",
        "cache-warm-up",
        "hosted-service-start"
    ];

    private static readonly HashSet<string> ForbiddenWorkloadNameSet = new(ForbiddenWorkloadNames, StringComparer.Ordinal);
    private readonly List<string> events = [];
    private readonly List<string> allowedWorkloadsBeforeLoginVisible = [];
    private readonly List<string> forbiddenWorkloadsBeforeLoginVisible = [];

    public IReadOnlyList<string> Events => events;

    public IReadOnlyList<string> AllowedWorkloadsBeforeLoginVisible => allowedWorkloadsBeforeLoginVisible;

    public IReadOnlyList<string> ForbiddenWorkloadsBeforeLoginVisible => forbiddenWorkloadsBeforeLoginVisible;

    public void RecordAllowedWorkloadBeforeLoginVisible(string workloadName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workloadName);

        if (ForbiddenWorkloadNameSet.Contains(workloadName))
        {
            forbiddenWorkloadsBeforeLoginVisible.Add(workloadName);
            return;
        }

        allowedWorkloadsBeforeLoginVisible.Add(workloadName);
    }

    public void RecordForbiddenWorkloadBeforeLoginVisible(string workloadName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workloadName);

        forbiddenWorkloadsBeforeLoginVisible.Add(workloadName);
    }

    public void RecordPreAuthMetadataComplete()
    {
        events.Add("startup.pre-auth-metadata-complete");
        RecordAllowedWorkloadBeforeLoginVisible("pre-auth-metadata");
    }
}
