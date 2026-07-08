# Sprint 11 Installer Verification

Status: HD-S11-003 RC package build/sign evidence produced; release-ready installer closure remains blocked until install/uninstall/upgrade and clean-profile evidence passes.

## Task 5 Authority Stance

- Approved combination: `D1-3R conditional, D2-1R, D3-1, D4-1, D5-1R, D6-1, D7-3`.
- MSIX package identity for RC mechanics: `Dobby`.
- Display name: `Dobby`.
- `Dobby` as RC-only identity is not a GA/customer package identity commitment.
- Reject package identities `Dobby.Host` and `Dobby.RC`.
- Executable and project names may remain internal implementation names, but they must not define the MSIX package identity.
- Self-signed RC publisher/certificate package family does not promise GA/customer commercial-certificate package family continuity.
- RC `vN -> vN+1` upgrade evidence proves only the same RC identity/publisher/certificate line.
- GA upgrade path requires separate authority and evidence.
- Packaging tooling stance: Windows SDK `MakeAppx` and `SignTool`, with installed versions and paths pinned in evidence before packaging.
- Signing stance: self-signed certificate is RC mechanics only.
- Versioning stance: `vN+1` must monotonically increase under the approved MSIX package version policy and share the same RC identity/family as `vN`.
- Deterministic install/upgrade evidence requires a snapshot-based clean VM/profile.
- MSI fallback remains dormant unless MSIX is blocked and fallback is separately approved.

## AC-024

AC-024 requires installer install/uninstall/upgrade and data-preservation evidence for the approved release installer path.

## RC Package Evidence

- Script: `installer\msix\build-rc-msix.ps1 -NoRestore`
- Tooling: `WindowsKits10-x64 MakeAppx.exe 10.0.18362.1 (WinBuild.160101.0800)`
- Tooling: `WindowsKits10-x64 SignTool.exe 4.00 (WinBuild.160101.0800)`
- Certificate subject: `CN=Dobby RC Self-Signed Publisher`
- Certificate thumbprint: `F4194E048AFF7D2561316CC4D73E5E966C40461F`
- Certificate store: `CurrentUser\My`
- Package vN: `artifacts\packages\msix\rc\Dobby_1.0.0.0_x64_RC.msix`
- Package vN SHA-256: `9a6b9427d27039c660cf4123867cb7f2b9eaf4322d07bab97a817ea8f8550e19`
- Package vN+1: `artifacts\packages\msix\rc\Dobby_1.0.1.0_x64_RC.msix`
- Package vN+1 SHA-256: `8648e2c14d219db10a2ba2f7ae6d24691ddd88bb6d3876f1e7ad8d0ff10ad91b`
- Machine-readable summary: `artifacts\packages\msix\rc\summary.json`
- Hash list: `artifacts\packages\msix\rc\sha256.txt`

These artifacts prove the approved RC identity/publisher/certificate line, the vN `1.0.0.0` to vN+1 `1.0.1.0` version increment, and successful local MSIX build/sign mechanics only.

## Blocked Fields

| Field | Status |
| --- | --- |
| MSIX package identity evidence | PRODUCED: both RC packages carry identity `Dobby` on the same RC publisher/certificate line |
| Display name evidence | PRODUCED: both RC packages were staged with display name `Dobby` |
| Signing certificate evidence | PRODUCED: `CN=Dobby RC Self-Signed Publisher`, thumbprint `F4194E048AFF7D2561316CC4D73E5E966C40461F` |
| Versioning policy evidence | PRODUCED: `1.0.0.0 -> 1.0.1.0` under the same RC identity/publisher/certificate line |
| Packaging tooling evidence | PRODUCED: `WindowsKits10-x64 MakeAppx.exe 10.0.18362.1`, `WindowsKits10-x64 SignTool.exe 4.00` |
| Installer artifact hash | PRODUCED: see the RC package SHA-256 values above |
| Install/uninstall result | NOT RUN |
| Upgrade data preservation | NOT RUN |
| Clean VM/profile evidence | NOT RUN |
| Self-signed certificate GA/customer signing readiness | NOT CLAIMED |

## Remaining Blockers

- Clean install, uninstall, and upgrade were not run on a snapshot-based clean VM/profile.
- AC-024 data-preservation claims remain unproven until `vN -> vN+1` install/upgrade is executed against preserved host data on a clean profile.
- RC self-signed certificate trust bootstrap for install testing is not yet recorded; `signtool verify /pa` currently reports an untrusted root for the RC certificate line.
- RC evidence does not activate MSI fallback and does not claim GA/customer signing readiness or package-family continuity.
