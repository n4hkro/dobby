# Skeleton Auth KDF Selection

- Selected skeleton algorithm: PBKDF2-HMAC-SHA256.
- Reason: Argon2id remains deferred until a human-approved Tier 2 package pin, package-health/license check, and latency evidence exist.
- Third-party KDF package: none.
- PBKDF2 floor: 600000 iterations.
- Salt floor: 16 bytes.
- Hash length: 32 bytes.
- Local measurement command: .NET `Rfc2898DeriveBytes.Pbkdf2` with 600000 iterations, SHA-256, 16-byte random salt, 32-byte output.
- Skeleton-local measured latency: 97.99 ms on this workstation.
- Scope: skeleton-local evidence only; release-candidate evidence must be regenerated under the current build id.

## Sprint 5 Implementation Complete

- Runtime wiring: `KdfPolicyResolver` resolves `AuthOptions.PasswordKdfMode = Pbkdf2Sha256` to PBKDF2-HMAC-SHA256.
- Credential storage: `CredentialStore` creates, verifies, and rebinds protected password hash material through the Sprint 4 protected-data primitive.
- Sprint 5 package decision: Argon2id remains deferred; no Argon2 package pin was added.
- PBKDF2 implementation parameters verified by tests: 600000 iterations, 16-byte salt, 32-byte hash, SHA-256.
- Verification evidence: `Dobby.UnitTests` passed with 199 tests / 0 failures; `Dobby.IntegrationTests` passed with 78 tests / 0 failures before fixture evidence expansion, and targeted recovery fixture tests passed with 7 tests / 0 failures after fixture materialization.
- Sprint 5 final closure verification on 2026-05-19 passed with 333 total tests, 0 failures; the PBKDF2-HMAC-SHA256 package decision remains unchanged. Credential protected material now derives DPAPI entropy from runtime `ApplicationEntropy` plus user credential scope, and tampered protected credential unwraps fail closed through a typed result.
