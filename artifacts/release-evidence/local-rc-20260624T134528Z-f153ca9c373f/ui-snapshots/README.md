# Sprint 11 UI Snapshot Evidence

Status: LOCAL AUTOMATED SNAPSHOT DESCRIPTORS PASSED; manual DPI evidence remains out-of-band.

## Automated Matrix

`tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs` covers rendered shell surfaces and descriptor evidence for:

- 1366x768 at 100 percent DPI equivalent.
- 1920x1080 at 100 percent and 125 percent DPI equivalent.
- 2560x1440 at 100 percent DPI equivalent.
- 3840x2160 at 100 percent DPI equivalent.
- Light and dark themes.
- Top toolbar, sidebar, third bar, main content, bottom status, minimized work strip, plugin license surface, feedback states, and background-operation surface.

## Accessibility And Responsiveness

- `ShellUiStaticScanTests.cs` verifies required shell regions, focus/motion tokens, contrast floors, first-paint marker, and background-operation wiring.
- `ShellKeyboardSmokeTests.cs` covers keyboard traversal and accessible action surfaces.
- Sprint 11 added static coverage for license-import background-operation reporting.

## Manual Evidence

125 and 150 percent DPI manual smoke may supplement this automated baseline when a release owner requires screenshots. No bitmap screenshots are claimed in this local evidence package.
