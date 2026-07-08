# Sprint 11 Production Recovery Handover

Status: HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES.

## Authority

- D-31: Production/Admin recovery evidence was deferred to Sprint 11.
- D-33: Sprint 10 technical-path proof is not production/Admin AC-007 release evidence.
- ADR-0033 controls production signing key ownership and custody.
- `src/Dobby.Host/Trust/README.md` requires Sprint 8 bootstrap keys to rotate before production release.
- D-35 approves the production key/recovery handover path only; it does not close P1/release-ready until evidence passes.
- Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

## Current Hash State

- source `vendor-master-public-key.pem` SHA-256: `00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f`
- Sprint 8 bootstrap production hash: `00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f`
- Result: blocking equality. The current source PEM is still the bootstrap public key and cannot close production handover.

## Missing Required Production Evidence

| Required proof | Status |
| --- | --- |
| Approved GA-candidate public key | BLOCKED |
| Custody public-key hash | BLOCKED |
| Source/embedded/custody hash equality | BLOCKED |
| Embedded resource byte extraction from published `Dobby.Security.dll` | BLOCKED |
| Admin-generated production `.dobbyrecovery` | BLOCKED |
| Published Host import result | BLOCKED |
| Redacted Admin report/transcript hash chain | BLOCKED |
| Fail-closed redaction scan | BLOCKED |

No production private key material is stored in this evidence root. No signing env var names, values, passphrases, private key paths, raw private material, raw/canonical recovery payloads, stack traces, or raw Admin transcripts are stored in this evidence root.
