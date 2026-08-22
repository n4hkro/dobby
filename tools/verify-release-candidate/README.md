# Release candidate evidence runner

The runner requires a new/empty evidence directory and a clean tracked source
tree. It records command, duration, exit code, raw log size and SHA-256 for
format, Release build, unit/integration/smoke, flake-sensitive/security suites,
SBOM/vulnerability evidence and the exact versioned portable artifact. Silent
commands still receive an empty, hashed log. Supplied external evidence is
copied into the evidence directory before its size and SHA-256 are recorded.

```powershell
.\tools\verify-release-candidate\verify-release-candidate.ps1 `
  -NoRestore `
  -EvidenceRoot .\artifacts\release-evidence\<new-run-id>
```

Full release mode fails unless exact installed-MSIX upgrade, production signing,
license-review and signed-sample verification inputs are supplied. `-SourceOnly`
runs every repository-owned gate but records those missing owner/environment
inputs as blockers; it never labels that result GA/release-ready.

Each external input must be UTF-8 JSON conforming to
`docs/schemas/release-external-evidence-v1.schema.json`. The runner additionally
checks the exact gate, candidate commit, lowercase subject SHA-256 values and
the gate-specific claim set before copying the evidence into the run directory.
Required claims are defined by `validate-external-evidence.ps1`; an arbitrary or
empty file cannot satisfy a release gate.
