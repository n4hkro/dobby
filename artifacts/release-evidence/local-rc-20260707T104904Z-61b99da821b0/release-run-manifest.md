# Sprint 11 Release Run Manifest

Build ID: local-rc-20260707T104904Z-61b99da821b0
Evidence root: artifacts\release-evidence\local-rc-20260707T104904Z-61b99da821b0
Source commit full SHA: 61b99da821b02d6c237dc55f14946bc8d298dc65
Source commit short SHA: 61b99da821b0
Source branch: main
Pre-run source config state:
```text
## main...origin/main
 M SPRINT_11_EXECUTION_PLAN.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/auth-kdf-selection.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/installer-verification.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/packaging-static-scan.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/production-recovery-handover.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/release-run-manifest.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/release-smoke.md
 M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/startup-performance.md
 M artifacts/release-evidence/skeleton-local/decision-log.md
 M src/Dobby.UI/ThemeTokens.cs
 M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
 M tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs
 M tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs
?? docs/superpowers/plans/2026-07-07-p1-release-gate-closure.md
```

Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

Repository CI status: repository CI ownership is absent; equivalent local release gate human-approved.

Release readiness: BLOCKED UNTIL EVIDENCE PASSES

## Final Local Verification Addendum

- Final build/test/publish evidence: `unit-integration-smoke-results.md`.
- Local format, release build, unit, integration, smoke, and portable publish checks passed in this evidence pass.
- These checks do not close release readiness while Task 4 and Task 5 evidence blockers remain open.

## Task 3 Trust And Recovery Addendum

- DobbyPluginTrustProfile=Production
- sourceVendorPublicKeyHash: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f`
- embeddedVendorPublicKeyHash: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f`
- custodyVendorPublicKeyHash: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f` (same locally generated GA-candidate key as source/embedded; not an independent custodian value)
- independentCustodyEqualityResult: NOT PROVEN
- oldBootstrapProductionHash=00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f
- oldBootstrapNegativeControlResult: PASS
- publishedRecoveryImportResult: PASS
- redactionGate: PASS
- HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES. This addendum proves source/embedded anchor equality, bootstrap negative control and a local published-Host import against a locally generated GA-candidate key. It does not prove authorized-custodian handover, so HD-S11-001 stays open and the production trust anchors remain pre-GA placeholders.

## Task 4 Startup Performance Authority Addendum

- `HD-S11-002` approved combination: `D1-1, D2-1-after-measurement, D3-1R, D4-1`.
- `docs/_perf/baselines/v1.0.0.json` is not created in this pass and may be created only after measurement completes and release authority accepts the baseline artifact.
- The future accepted baseline artifact must include profile id, hardware/OS specification, measurement method, raw artifact hashes, P95 values, and baseline comparison source/hash.
- Measurement must bind to the existing startup instrumentation and milestones.
- Raw JSONL samples and `summary.json` must be hashed.
- Manual or video fallback is not sufficient for the release gate.
- Current local machine evidence remains disqualification evidence only for ADR-0042 closure.
- Task 4 authority is durable, but startup-performance evidence remains blocked pending an ADR-0042-compliant baseline machine/profile and N=100 samples.

## Task 5 Installer Authority Addendum

- `HD-S11-003` approved combination: `D1-3R conditional, D2-1R, D3-1, D4-1, D5-1R, D6-1, D7-3`.
- MSIX package identity for RC mechanics: `Dobby`.
- Display name: `Dobby`.
- RC-only identity `Dobby` is not a GA/customer package identity commitment.
- Rejected package identities: `Dobby.Host`, `Dobby.RC`.
- Executable and project names may remain internal implementation names, but must not define the MSIX package identity.
- Packaging tooling evidence: `WindowsKits10-x64 MakeAppx.exe 10.0.18362.1 (WinBuild.160101.0800)` and `WindowsKits10-x64 SignTool.exe 4.00 (WinBuild.160101.0800)`.
- Self-signed certificate evidence is RC mechanics only and does not promise GA/customer certificate continuity.
- RC certificate evidence: subject `CN=Dobby RC Self-Signed Publisher`, thumbprint `F4194E048AFF7D2561316CC4D73E5E966C40461F`, store `CurrentUser\My`.
- `vN -> vN+1` upgrade evidence must stay on the same RC identity/publisher/certificate line and follow the approved MSIX version policy.
- RC package artifacts: `artifacts\packages\msix\rc\Dobby_1.0.0.0_x64_RC.msix` (`9a6b9427d27039c660cf4123867cb7f2b9eaf4322d07bab97a817ea8f8550e19`) and `artifacts\packages\msix\rc\Dobby_1.0.1.0_x64_RC.msix` (`8648e2c14d219db10a2ba2f7ae6d24691ddd88bb6d3876f1e7ad8d0ff10ad91b`).
- RC package summary: `artifacts\packages\msix\rc\summary.json`.
- Trust verification note: local `signtool verify /pa` against the RC packages reports an untrusted root until the RC self-signed certificate is trusted for install verification.
- Deterministic install/upgrade evidence requires a snapshot-based clean VM/profile.
- MSI fallback remains dormant unless MSIX is blocked and fallback is separately approved.
- Task 5 authority is durable; identity/signing/versioning/tooling package-build proof now exists, but installer evidence remains blocked pending clean-profile install/uninstall/upgrade and data-preservation proof.

Task 5 execution command in this evidence pass: `.\installer\msix\build-rc-msix.ps1 -NoRestore`
