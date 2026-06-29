namespace Dobby.Shell.WorkspaceTabs;

public sealed record ShellWorkspaceTab(
    Guid TabId,
    string Title,
    string RouteKey,
    bool IsActive,
    bool CanClose);
