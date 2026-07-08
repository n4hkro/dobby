# Installer

Ownership path for the future MSIX primary installer and MSI fallback.

Concrete packaging, upgrade, rollback, and release-candidate evidence are deferred
until publish/installer scope opens.

Current Sprint 11 release authority fixes the RC-only installer stance as follows:

- Primary installer path remains MSIX for RC mechanics evidence.
- RC MSIX package identity: `Dobby`.
- RC display name: `Dobby`.
- `Dobby` is an RC-only identity decision, not a GA/customer package identity commitment.
- Rejected RC package identities: `Dobby.Host`, `Dobby.RC`.
- Executable/project names may remain internal implementation names, but must not define the MSIX package identity.
- Packaging tooling for RC evidence is Windows SDK `MakeAppx` and `SignTool`, with installed versions/paths pinned in evidence before packaging.
- Self-signed certificate use is RC mechanics only; it does not imply GA/customer production signing readiness or package-family continuity.
- MSI fallback remains dormant unless MSIX is blocked and fallback is separately approved.

Current RC packaging command:

- `.\installer\msix\build-rc-msix.ps1 -NoRestore`

Current RC packaging outputs:

- `artifacts\packages\msix\rc\Dobby_1.0.0.0_x64_RC.msix`
- `artifacts\packages\msix\rc\Dobby_1.0.1.0_x64_RC.msix`
- `artifacts\packages\msix\rc\summary.json`
- `artifacts\packages\msix\rc\sha256.txt`
