# Sprint 11 Auth KDF Selection

Status: BLOCKED PENDING HUMAN DECISION for release-ready AC-051 closure because ADR-0042 baseline latency evidence is not approved or collected.

## AC-051 Evidence

- Selected algorithm: PBKDF2-HMAC-SHA256.
- PBKDF2 floor: 600000 iterations.
- Salt floor: 16 bytes.
- Hash length: 32 bytes.
- Third-party KDF package: none.
- Package decision: no new package pin was added.
- Argon2id remains deferred until a human-approved package pin, health/license review, and accepted baseline latency evidence exist.

## Verification

- `tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs` is the automated policy guard for the selected mode and floors.
- Baseline Release unit run passed with 622 tests, 0 failures.
- No Argon2 package appears in `Directory.Packages.props`.

## Boundary

This file regenerates the skeleton-local KDF decision for Sprint 11 local evidence. It does not close AC-051 release readiness because HD-S11-002 remains blocked.
