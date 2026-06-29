# Portable Folder Publish Design

## Goal

Create a local support/dev portable publish flow that produces an ordinary folder, not a zip, so Dobby can be launched from `Dobby.Host.exe` for manual testing.

## Scope

The flow targets `Dobby.Host` only. It creates a Release `win-x64` self-contained publish output under `artifacts/publish/portable/Dobby.Host/`. The output remains a local artifact and does not become the primary customer deployment path. MSIX remains the primary customer deployment format per ADR-0047.

## Architecture

Add a PowerShell wrapper at `tools/publish-portable/publish-portable.ps1`. The wrapper owns repeatable command-line behavior: resolve the repo root, optionally restore, remove the previous portable output folder unless requested otherwise, run `dotnet publish`, and verify required files exist.

The script does not create archives. It prints the portable folder path and executable path after verification.

## Error Handling

The script uses strict PowerShell settings and stops on command failures. After publish, it checks for required files:

- `Dobby.Host.exe`
- `Dobby.Host.dll`
- `appsettings.json`
- `native/win-x64/sqlite3mc_x64.dll`

If any required file is missing, the script fails with a clear message.

## Testing

Add an integration/architecture contract test that pins the portable script behavior:

- the script exists at the expected path;
- the script publishes to `artifacts/publish/portable/Dobby.Host`;
- the script uses Release, `win-x64`, and self-contained publish settings;
- the script does not call zip/archive commands;
- the script verifies `Dobby.Host.exe` and the native SQLite DLL.

Manual verification runs the script and confirms the folder is produced.
