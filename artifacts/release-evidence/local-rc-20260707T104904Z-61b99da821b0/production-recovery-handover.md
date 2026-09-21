# Production Recovery Handover

HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES

Source public-key SHA-256: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f`
Embedded public-key SHA-256: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f`
Custody public-key SHA-256: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f`
Source/embedded equality: PASS
Independent custody equality: NOT PROVEN
Custody provenance: the recorded custody hash is the same locally generated GA-candidate key that produced the source and embedded anchors, so the three values match by construction and prove no authorized-custodian handover. HD-S11-001 closure requires a custody hash supplied independently of this repository by the release-authority custodian.
Custodian: `redacted-local-rc-agent-custodian` (local RC agent, not an authorized production custodian)
Rotation disposition: Sprint 8 bootstrap trust key rotated to agent-generated GA-candidate production public key for this local RC; bootstrap key retained only as negative-control evidence.
Bootstrap public-key negative control: PASS, hash differs from production public key.
Private key handling: PASS, not present in repo/source/tests/evidence.

## Redacted Admin Evidence

Recovery response command: `create-recovery-response`
Signing profile: `production`
Signing key provenance: locally generated GA-candidate key material, not vendor production custody material; this evidence therefore proves RC mechanics only
Command result: exit code 0 (PASS)
.dobbyrecovery artifact SHA-256: `a15677bfb4f4f0ad12d4a381805f74e7948d87909f25c9c8a420b3fcad9628ca`
Redacted Admin report SHA-256: `908865e5b13e1d2a38b5020d68424f750c2af2d6963b633aada260c8ca60e330`
Redacted transcript hash-chain digest: `26b8859c8c8650d85f389d24b6c4584538755e7aa180d04e651a07bf882f9a66`
Redacted transcript hash-chain refs: `command=create-recovery-response` -> `profile=production` -> `exit=0` -> `stdout-sha256` -> `stderr-sha256` -> `report-sha256` -> `artifact-sha256`

## Published Host Import

Published Host path: `src\Dobby.Host\bin\Release\net10.0-windows\publish\Dobby.Host.exe`
Clean temp import context: PASS
Published Host import result: PASS
Deterministic success signal: `RecoveryResponseImportCoordinator.VerifyFile=PASS`
Imported artifact SHA-256: `a15677bfb4f4f0ad12d4a381805f74e7948d87909f25c9c8a420b3fcad9628ca`
