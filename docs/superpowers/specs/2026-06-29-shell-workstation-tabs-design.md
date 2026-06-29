# Shell Workstation Tabs Design

## Goal

Redesign the Dobby shell around Chrome-like workstation tabs while keeping the app startup and new-tab experience anchored on Home. The first implementation phase is shell-first: tab lifecycle, custom titlebar, centered top navigation, collapsible sidebar, and close confirmation. Full visual polish for Home, Settings, Plugins, and future workstation content is out of scope for this first phase.

## Current Context

Dobby is a WPF desktop app. The current host shell is mostly implemented in `src/Dobby.Host/MainWindow.xaml` and `src/Dobby.Host/MainWindow.xaml.cs`, with route and shell state in `src/Dobby.Shell/ShellViewModel.cs`. Work-session lifecycle primitives already exist in `src/Dobby.Shell/WorkSessions/WorkSessionRegistry.cs` and `src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs`.

The current UI uses a normal window titlebar, an in-window top toolbar, a left sidebar block, a third bar, and a main content surface. The redesign will turn the window titlebar area into the workstation tab row and make the shell layout closer to the provided CRM reference: one continuous light background, centered top navigation, and a sidebar that does not read as a separate colored panel.

## Chosen Approach

Use a shell-first redesign.

This approach changes the shell model first instead of only restyling XAML. `ShellViewModel` will own workstation tab state and route state per tab. WPF will bind to that model. This is safer than a visual-only redesign because tab creation, tab title updates, tab close behavior, and route persistence become testable behavior rather than implicit UI state.

The chosen titlebar style for the first phase is a light CRM titlebar. Future visual variants should be handled through theme tokens and must not change the tab lifecycle model.

## Workstation Tab Behavior

Each top titlebar tab is a workstation container.

On app startup, Dobby creates exactly one active tab:

- Route: Home
- Title: Home
- Active: true

The `+` button always creates a new active tab with:

- Route: Home
- Title: Home

Tab titles follow the active content inside that tab:

- Home route sets the tab title to Home.
- Settings route sets the tab title to Settings.
- Plugins route sets the tab title to Plugins.
- A real plugin or workstation page sets the tab title to that page or work item name.

Each tab keeps its own current route. Switching tabs restores that tab's route and content. Navigating in the centered top navigation or sidebar updates only the active tab.

## Tab Close Behavior

Tab close uses these rules:

- If one tab remains and the user closes it, the app does not close. The tab is replaced by a new Home tab.
- If two or more tabs exist and a non-leftmost tab closes, the tab immediately to its left becomes active.
- If two or more tabs exist and the leftmost tab closes, the next tab on the right becomes active because no left tab exists.

Closing a tab with an active plugin or workstation task must keep the existing work-session warning flow. If the user cancels that warning, the tab remains open. If the user chooses close, the tab closes according to the rules above. If the user chooses minimize where that choice is available, the work session is minimized and navigation/close continues according to the existing coordinator behavior.

## Window Close Behavior

The main window `X` does not close only the active tab. It initiates whole-app close.

When the user clicks the main window `X`, Dobby shows a confirmation dialog. If the user confirms, the app closes. If the user cancels, the app remains open and no tab state changes.

If active work sessions require the existing close/logout warning flow, that flow must remain in effect before final shutdown.

## Visual Layout

The shell uses one continuous light background. The sidebar and centered top toolbar do not get separate block backgrounds or divider borders. This is an explicit design rule from the reference image.

Allowed shell highlights:

- Active sidebar item can use a subtle translucent pill.
- Active top navigation item can use a pill.
- Individual content groups can use soft white or translucent surfaces.

Not allowed in the first phase:

- Separate solid background block for the sidebar.
- Separate solid background block for the top toolbar.
- Divider line between sidebar and content just to make the sidebar read as a panel.
- A dark Chrome strip as the default first-phase titlebar.

The titlebar row contains workstation tabs, a `+` button, and the window controls. The app content area below contains the sidebar, centered top navigation, optional right-side action icons, and the active route content.

The sidebar behavior:

- Expanded: brand mark plus app name at the top, then icon plus item label for each item.
- Collapsed: brand mark and item icons only, preserving the same order and actions.

The top navigation behavior:

- Top navigation items are centered in the content area.
- The brand mark and app name do not occupy the centered navigation area; they live in the sidebar.
- The top navigation should not shift horizontally when small right-side actions appear, so the layout needs balanced columns or equivalent constraints.

## Component Boundaries

The shell model should expose focused concepts instead of embedding all behavior in `MainWindow.xaml.cs`.

Recommended model additions:

- `ShellWorkspaceTab`: immutable or simple record-like state for tab id, title, route key, active state, and close availability.
- `ShellViewModel.WorkspaceTabs`: ordered visible tabs.
- `ShellViewModel.ActiveWorkspaceTab`: current tab.
- Commands or methods for `OpenWorkspaceTab`, `ActivateWorkspaceTab`, `CloseWorkspaceTab`, and active-tab navigation.

WPF responsibilities:

- Render tab strip.
- Render sidebar expanded/collapsed states.
- Render centered top toolbar.
- Delegate tab and navigation actions to the view model.
- Show window-close confirmation and existing work-session warnings.

The existing `WorkSessionRegistry` and `WorkSessionCloseCoordinator` remain the work-session lifecycle authority. The workstation tab model should not duplicate plugin runtime lifecycle rules.

## Data Flow

Startup flow:

1. `App.xaml.cs` creates `ShellViewModel`.
2. `ShellViewModel` initializes one Home workspace tab.
3. `MainWindow` binds tab strip and content surfaces to the view model.
4. First paint still happens before deferred plugin activation work.

Navigation flow:

1. User clicks a sidebar or centered top nav item.
2. `MainWindow` asks the existing `WorkSessionCloseCoordinator` if navigation needs a warning.
3. If no warning is needed, `ShellViewModel` updates the active tab route and title.
4. If warning is needed, the warning panel appears and the user's choice determines whether navigation proceeds.

Tab close flow:

1. User clicks a tab close button.
2. The shell checks whether the tab owns or is tied to an active work session.
3. If warning is needed, the existing warning flow controls the result.
4. If close proceeds, `ShellViewModel` removes or replaces the tab and selects the next active tab using the approved close rules.

Window close flow:

1. User clicks main window `X`.
2. Confirmation dialog appears.
3. Confirm closes the whole app after existing work-session close rules are satisfied.
4. Cancel leaves the app unchanged.

## Error Handling

Tab lifecycle operations should be fail-safe:

- The view model must never expose zero tabs after a close operation.
- Invalid tab ids should fail without changing the active tab.
- If a tab close is cancelled by a work-session warning, no tab ordering or active tab changes occur.
- If route activation fails, the tab title and route stay unchanged.

Window close confirmation cancellation must leave all tabs, active route state, and work-session state unchanged.

## Accessibility and Keyboard Behavior

The custom titlebar must preserve predictable keyboard and accessibility behavior:

- Tabs need stable accessible names based on their title.
- The `+` button needs a localized accessible name for opening a new Home workspace tab.
- Tab close buttons need localized accessible names.
- Minimize, maximize, and close controls must remain keyboard reachable.
- Focus order should move through titlebar tabs, `+`, window controls, sidebar, centered top navigation, then content.

## Testing Strategy

Use TDD for behavior changes.

Unit tests:

- Startup creates one active Home tab.
- `+` creates a new active Home tab.
- Navigating the active tab updates that tab's route and title without changing other tabs.
- Closing the only tab replaces it with a new Home tab.
- Closing a tab with tabs on the left activates the immediate left tab.
- Closing the leftmost tab activates the next tab on the right.
- Cancelled work-session warning leaves tabs unchanged.

WPF smoke and integration tests:

- The shell renders `WorkspaceTabStrip`, `WorkspaceNewTabButton`, sidebar, centered top toolbar, and content host.
- The sidebar supports expanded icon-plus-label and collapsed icon-only states.
- Home, Plugins, and Settings navigation operate inside the active tab.
- The main window close path requests confirmation instead of closing immediately.

Static scan tests:

- Shell XAML declares required named regions and bindings.
- Shell XAML does not introduce hardcoded visible strings.
- Shell XAML does not introduce separate sidebar or top-toolbar block backgrounds.
- Shell XAML still uses theme tokens rather than hardcoded colors, margins, and font sizes.

## Out of Scope

- Full redesign of Home, Settings, Plugins, Reports, and plugin content internals.
- Dark titlebar variant as the default first-phase design.
- Persisting all tabs across app restarts.
- Browser-like drag-to-reorder tabs.
- Browser-like address bar or URL concepts.

## Acceptance Criteria

- App startup always shows one active Home tab.
- `+` always opens a new active Home tab.
- Tab title tracks active content according to the agreed C behavior.
- Closing the last tab creates a new Home tab instead of closing the app.
- Closing any tab when multiple tabs exist activates the immediate left tab, or the next right tab when closing the leftmost tab.
- Main window `X` asks for confirmation before closing the app.
- Sidebar and top toolbar sit on one continuous shell background.
- Expanded sidebar shows brand plus labels; collapsed sidebar shows only brand and icons.
- Top navigation is centered in the content area.
- Existing work-session close/minimize/cancel warnings continue to protect active work.
