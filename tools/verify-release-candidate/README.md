# Release candidate evidence runner

The runner requires a new/empty evidence directory and a clean tracked source
tree. It records command, duration, exit code and raw log for format, Release
build, unit/integration/smoke, flake-sensitive/security suites, SBOM/vulnerability
evidence and the exact versioned portable artifact.

```powershell
.\tools\verify-release-candidate\verify-release-candidate.ps1 `
  -NoRestore `
  -EvidenceRoot .\artifacts\release-evidence\<new-run-id>
```

Full release mode fails unless exact installed-MSIX upgrade, production signing,
license-review and signed-sample verification inputs are supplied. `-SourceOnly`
runs every repository-owned gate but records those missing owner/environment
inputs as blockers; it never labels that result GA/release-ready.
