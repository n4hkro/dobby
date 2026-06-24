# Sprint 11 Storage Provider Proof

Status: LOCAL RELEASE EVIDENCE WITH HUMAN-GATED RELEASE READINESS.

## Provider

- Provider mode: SQLite3MultipleCiphers.
- Native asset: `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll`.
- Native SHA-256: `3744689bf8427b40b222c1ec046ebc5e4f08c5c9898d56573112006ff2c4def8`.
- Cipher profile authority: ADR-0054 and Sprint 4 storage materialization evidence.
- No storage-provider switch was introduced in Sprint 11.
- No unprotected DB fallback is approved.

## Automated Evidence

- `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`
- `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs`
- `tests/Dobby.IntegrationTests/Infrastructure/ProtectedMaterialDpapiTests.cs`
- `tests/Dobby.IntegrationTests/Security/NoPlaintextSecretContentScanTests.cs`
- `tests/Dobby.SmokeTests/CrashRecoverySmokeTests.cs`

Baseline Release integration and smoke runs passed before Sprint 11 evidence generation. Final no-restore verification is recorded in `unit-integration-smoke-results.md`.

## Boundary

No storage/KDF/provider policy changed in Sprint 11. Storage operation heartbeat evidence is owned by host orchestration and background-operation tests, not by a low-level provider rewrite.
