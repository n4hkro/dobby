# Portable Folder Publish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a repeatable local command that publishes Dobby as an ordinary portable folder for manual testing.

**Architecture:** A PowerShell wrapper in `tools/publish-portable/` runs `dotnet publish` for `Dobby.Host` with Release, `win-x64`, and self-contained settings. A contract test in integration architecture tests pins the script path and key behavior so it cannot drift into zip packaging.

**Tech Stack:** .NET 10, WPF, xUnit, Shouldly, PowerShell.

---

### Task 1: Portable Publish Script Contract

**Files:**
- Create: `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`
- Create: `tools/publish-portable/publish-portable.ps1`
- Create: `tools/publish-portable/README.md`
- Modify: `docs/09_BUILD_RUN_AND_RELEASE.md`

- [ ] **Step 1: Write the failing test**

Create a test that reads `tools/publish-portable/publish-portable.ps1` and verifies the path, output folder contract, publish settings, no archive command, and required file checks.

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PortablePublishScriptTests"
```

Expected: fail because the script does not exist yet.

- [ ] **Step 3: Write minimal implementation**

Add the script and README. Update the build/run/release doc with the command:

```powershell
.\tools\publish-portable\publish-portable.ps1
```

- [ ] **Step 4: Run test to verify it passes**

Run the same filtered test command. Expected: pass.

- [ ] **Step 5: Run real publish verification**

Run:

```powershell
.\tools\publish-portable\publish-portable.ps1
```

Expected: the script prints `Portable folder:` and `Executable:` paths, and `artifacts/publish/portable/Dobby.Host/Dobby.Host.exe` exists.

- [ ] **Step 6: Run broader verification**

Run:

```powershell
dotnet build .\Dobby.sln -c Debug --no-restore
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Debug --no-build --filter "FullyQualifiedName~PortablePublishScriptTests"
```

Expected: build succeeds and the contract test passes.
