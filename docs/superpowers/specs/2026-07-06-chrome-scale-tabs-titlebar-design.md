# Chrome-Scale Workstation Tabs Titlebar Design

## Goal

Adjust the Dobby shell titlebar so its workstation tabs match the provided Chrome reference in spacing, proportions, and control placement.

## Approved Visual Behavior

Tabs use a Chrome-like titlebar strip:

- Tab height remains compact and close to the reference titlebar height.
- Tab width is long at normal desktop widths, about Chrome-scale rather than narrow pill-scale.
- Tabs shrink when available width is constrained instead of overflowing under the caption area.
- Each tab owns its close button inside the tab.
- The `+` new-tab button appears only once, immediately after the last visible tab.
- The `+` button does not appear inside every tab and does not float away from the final tab.
- The window minimize, maximize/restore, and close controls use default Windows caption buttons.

## Chosen Approach

Use native Windows caption buttons with WPF `WindowChrome` rather than maintaining hand-drawn caption buttons in XAML.

This keeps the app's custom draggable titlebar and tab strip while returning minimize, maximize/restore, and close to standard Windows behavior. The tab strip will reserve enough right-side space for the OS caption controls, but Dobby will no longer render its own caption button stack.

The tab sizing converters remain the layout authority. They will be tuned so the tab row accounts for titlebar width, caption reserve, and the single trailing new-tab button. Individual tabs will use a larger maximum width and a stable minimum width so labels, icons, and close buttons do not cause layout shifts.

## Implementation Boundaries

In scope:

- Update titlebar XAML in `src/Dobby.Host/MainWindow.xaml`.
- Remove the custom minimize/maximize/close button visuals from the titlebar.
- Enable native caption buttons through `WindowChrome`.
- Tune tab and new-tab dimensions through existing theme tokens and sizing converters.
- Add or update tests that verify tab width math and the single trailing `+` placement.
- Add static/rendered checks where practical for native-caption configuration and tab-row structure.

Out of scope:

- Rewriting workstation tab lifecycle behavior.
- Persisting tab sizes.
- Drag-to-reorder tabs.
- Browser address bar behavior.
- Changing non-titlebar shell content.

## Acceptance Criteria

- At normal desktop width, tab length visually matches the provided Chrome-scale reference.
- `+` is rendered once, directly after the final tab.
- Each tab renders one close button inside the tab.
- Minimize, maximize/restore, and close are native Windows caption buttons.
- Window dragging, double-click maximize/restore, and resize behavior continue to work.
- Existing workstation tab lifecycle tests remain green.
- A refreshed portable build is produced after verification.

## Testing Strategy

Use TDD for changed behavior:

- Add converter tests for Chrome-scale tab width and shrink behavior.
- Add a static XAML test for native-caption configuration and absence of custom caption button stack.
- Add or update rendered shell evidence for the tab strip and new-tab button placement where the existing WPF test harness can observe layout bounds.

Full verification should include focused unit/integration tests, solution build, and portable publish with `.\tools\publish-portable\publish-portable.ps1 -NoRestore` when local assets allow it.
