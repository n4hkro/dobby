using Dobby.Diagnostics;

namespace Dobby.Host.Startup;

public sealed record HostBootstrapResult(IReadOnlyList<StartupMilestone> Milestones);
