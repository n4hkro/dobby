# Sprint 5 Recovery Test Fixture Authority

Status: APPROVED - Sprint 5 recovery test fixture authority model was human-approved on 2026-05-15 by `n4hkro (architecture/security/release approver)`.

This file records the approved Sprint 5 TEST-ONLY key and fixture custody model for host-side Owner recovery import evidence. It approves the authority model, required evidence fields, and closure gates. It does not claim that fixture binaries or hashes already exist; implementation must fill the concrete inventory rows before Sprint 5 closure.

## Sources

- `SPRINT_IMPLEMENTATION_PLAN.md` - Sprint 5 host-side Owner recovery scope, fixture-validation exit condition, and later Sprint 10/11 production handover boundaries.
- `SPRINT_5_EXECUTION_PLAN.md` - B-01/B-02 approved fixture/key decision and AC-007 verification plan.
- `docs/adr/ADR-0033-signing-key-ownership.md` - vendor master public key compile-time resource authority.
- `docs/adr/ADR-0039-owner-password-recovery.md` - recovery request/response format, ECDSA P-256 signature validation, nonce, expiry, and device-bound import rules.
- `docs/specs/storage-and-data-protection.md` - canonical `%LocalAppData%/Dobby/recovery/` runtime path.

## Authority Boundary

This ledger approves TEST-ONLY fixture material for Sprint 5 tests only.

- Production vendor private keys are out of scope for Sprint 5.
- Production vendor public key release/security custody remains a later release gate.
- Test public key material must be excluded from production assemblies.
- Test private key material must never be committed to this repository.
- Sprint 5 cannot close until the inventory and proof fields below are filled with implementation evidence and `RecoveryResponseImportFixtureTests.cs` is green.

## Approved Model

| Layer | Approved Direction | Status |
| --- | --- | --- |
| Unit tests | Generate an ephemeral ECDSA P-256 keypair at test time, sign synthetic recovery payloads, and verify importer behavior through a fake `IVendorRecoveryPublicKey`. | APPROVED |
| Integration tests | Use a committed TEST-ONLY public key resource embedded in the integration-test assembly and pre-signed binary `.dobbyrecovery` fixtures committed under the test fixture tree. | APPROVED |
| Test private key | Keep out of repo. Record custodian, protection mechanism, and regeneration/rotation policy in this ledger. | APPROVED |
| Production key handover | Defer production vendor key infrastructure and release-candidate handover evidence to Sprint 11 or a later release-security gate. | APPROVED |

## Test Public Key Resource

| Field | Value | Status |
| --- | --- | --- |
| Resource path | `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/vendor-recovery-public-key.TEST-ONLY.txt` | COMPLETE |
| SHA-256 | `1a30c878be9f0d69adcfbad0a7244e19738a3c81fb9ca7eb2581a8b1c2455a19` | COMPLETE |
| Embed mechanism | Test assembly embedded resource or equivalent test-only resource path; must not be copied to production assemblies. | APPROVED |
| Consuming tests | `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs` | APPROVED |
| TEST-ONLY tag | Public key resource name and/or containing folder must include an explicit TEST-ONLY marker. | APPROVED |

## Fixture Set Inventory

| Fixture path | SHA-256 | Outcome covered | Status |
| --- | --- | --- | --- |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/signature-ok.TEST-ONLY.dobbyrecovery` | `5ce931a8d8a0192f84a0e38b2658af41fe30bb6e282030374709957493ccab2f` | signature-ok | COMPLETE |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/signature-fail.TEST-ONLY.dobbyrecovery` | `4f4a47f13c90b245608e2b9cd1fbbd67613bbe7a41676806ba6d18bb2e075d57` | signature-fail | COMPLETE |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/expired.TEST-ONLY.dobbyrecovery` | `7af9787ba842b0275b39bf38abdea393140d23d54fdaa35d04a16802afcd166d` | expired | COMPLETE |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/replay.TEST-ONLY.dobbyrecovery` | `9b41eabd81a2e1a15257deeccd4f4676f34b8aa4c50363f016400395558843fc` | replay / consumed nonce | COMPLETE |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/device-mismatch.TEST-ONLY.dobbyrecovery` | `bdd3f76403da4efd69d848a85ae9bf400d256a53c14426e744be255fb2b45ddc` | device-mismatch | COMPLETE |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/owner-missing.TEST-ONLY.dobbyrecovery` | `4db933131bc63026e97b2937f300f4c435df74776c3c82c3f01c3ad19959b29b` | Owner-missing fail-closed | COMPLETE |
| `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/cancel.TEST-ONLY.dobbyrecovery` | `d003349589ab70bb4729df3c0de8aed18a2d46befb5df7d9ff11d3c9e812e4a5` | cancel leaves nonce and credential state unchanged | COMPLETE |

## Test Private Key Custody

Do not record private key bytes, secret paths, secret phrases, or material that enables signing.

| Field | Value | Status |
| --- | --- | --- |
| Custodian | Sprint 5 implementation session generated TEST-ONLY fixture keypair; private half was held only in transient local process memory while fixture files were emitted. | COMPLETE |
| Storage location class | Out-of-repo maintainer-controlled test fixture signing location; no repository path may contain private key material. | APPROVED |
| Protection mechanism | Private key was not written to the repository; `.gitignore` continues to block `*.pem`, `*.key`, `*.pfx`, and `*.p12`; committed fixture files contain signatures and public key only. | COMPLETE |
| Rotation / regeneration policy | Regenerate keypair and all fixture binaries if recovery payload schema, canonical JSON rules, ECDSA format, or test private key custody changes. | APPROVED |
| Signing procedure evidence | Fixtures were generated with an ephemeral ECDSA P-256 keypair, P1363 fixed-field signatures over `RecoveryCanonicalJsonWriter` canonical payload bytes, then validated by `RecoveryResponseImportFixtureTests.cs`. | COMPLETE |

## TEST-ONLY Tagging Proof

- Public key resource path contains an explicit TEST-ONLY marker: COMPLETE - `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/vendor-recovery-public-key.TEST-ONLY.txt`.
- Public key resource is included only by test project default content handling under `tests/`; no production project item includes this path: COMPLETE.
- Production projects do not reference or copy the test public key resource: COMPLETE - path is under `tests/Dobby.IntegrationTests`.
- Fixture binary paths live under a test fixture directory and are not included in publish output: COMPLETE - all `.dobbyrecovery` fixture paths live under `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/`.
- `PackageGovernanceTests`, architecture tests, or build-output smoke evidence cover absence from production output where practical: COMPLETE - plugin/SDK boundary tests and source graph checks keep recovery fixtures and auth types test/host-side only.

## Replacement Procedure

If recovery fixture schema, canonical JSON encoding, signature encoding, key versioning, or test key custody changes:

1. Generate a new out-of-repo test ECDSA P-256 keypair or use the approved existing custody location.
2. Export the TEST-ONLY public key resource and record its repo path and SHA-256 above.
3. Re-sign every required fixture outcome and record each fixture path and SHA-256 above.
4. Re-run `RecoveryResponseImportFixtureTests.cs`.
5. Update `architecture-baseline.md` Sprint 5 Reconfirmation or the later release evidence file that consumes this ledger.

## Sprint 11 Release-Candidate Handover

Sprint 11 or the later release-security gate must replace the Sprint 5 TEST-ONLY fixture path with production vendor key evidence for release-candidate validation.

Required handover notes:

- Production vendor public key resource path and hash are recorded in the appropriate release-candidate evidence ledger.
- TEST-ONLY public key resource remains test-only and is not present in production publish output.
- Any Admin-generated `.dobbyrecovery` end-to-end evidence is recorded under the later sprint that owns `create-recovery-response`.
- Sprint 5 fixture binaries remain valid regression fixtures only; they do not prove production vendor custody.

## Closure Rule

Sprint 5 recovery import closure requires all of the following:

- This ledger remains `APPROVED`.
- Every implementation evidence field above is replaced with concrete evidence.
- `RecoveryResponseImportFixtureTests.cs` is green against the committed TEST-ONLY fixture set.
- The Sprint 5 Reconfirmation block links to this ledger and records the fixture evidence status.
