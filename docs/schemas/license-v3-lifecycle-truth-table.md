# License v3 Derived Lifecycle Truth Table

The signed authority contains only `granted` or `revoked` intent. Runtime state
is never accepted from the artifact and is derived on every evaluation.

Rules are evaluated from top to bottom:

| Intent / validation | Trusted time | Evaluation instant | Derived state |
|---|---|---|---|
| malformed, bad signature, bad purpose/key, binding failure or anti-rollback failure | any | any | Denied |
| revoked | any | any | Denied |
| granted | unhealthy or rollback detected | any | Denied |
| granted | healthy | `now < notBefore` | Pending |
| granted | healthy | `notBefore <= now <= expiresAt` | Active |
| granted | healthy | `expiresAt < now <= checked(expiresAt + gracePeriodDays)` | Grace |
| granted | healthy | `now > checked(expiresAt + gracePeriodDays)` | ReadOnlyExpired |

Timestamp ordering requires `issuedAt <= notBefore < expiresAt`. Grace addition
uses checked arithmetic; overflow is malformed and therefore Denied. Boundary
comparisons are exact UTC instants. An imported v2 `status` never overrides this
table and cannot create a new v3 grant.

`gracePeriodDays` is bounded to `0..90` days by
[`dobby-plugin-license-v3.schema.json`](dobby-plugin-license-v3.schema.json) and
`docs/specs/license-import.md` § `Grace ceiling`. The host re-applies that ceiling
independently of the artifact, so a correctly signed value above `90`, a negative
value or a non-integer value is malformed and never widens the `Grace` row. A
deployment policy may shorten the effective grace window but may never extend it
beyond the signed value or the ceiling.

## Derived state to plugin-visible runtime mode and UI status

The derived state above is host-internal. The plugin-visible surface is the
`PluginRuntimeMode` enum owned by `ADR-0053`; the user-facing status vocabulary
is separate and localized. This mapping carries forward the mapping previously
held by superseded `ADR-0051 § 8a`:

| Derived state | Plugin-visible runtime mode | UI/license status | Notes |
|---|---|---|---|
| no stored license | `Denied` | `NoLicense` | Absence of an artifact, not a signed state. Plugin gets no runtime access; the host-level import flow stays available. |
| `Pending` | `Denied` | `Pending` | `notBefore` not reached; plugin access is non-disclosing deny. |
| `Active` | `FullAccess` | `FullAccess` | Normal operation. |
| `Grace` | `FullAccess` | `Grace` | Plugin runs; user warning and audit warning are raised. |
| `ReadOnlyExpired` | `ReadOnlyExpired` | `ReadOnlyExpired` | Historical render/read allowed; mutation, plugin/domain export, report/file output, print-to-file and background task start are denied. |
| `Denied` | `Denied` | `Denied` | Signature, key purpose, binding, anti-rollback, revocation or trusted-time failure; non-disclosing deny. |

`FullAccess`, `ReadOnlyExpired` and `Denied` are the only code-facing
`PluginRuntimeMode` values. `NoLicense`, `Pending` and `Grace` belong to the
UI/license status vocabulary and are never exposed as runtime enum values. The
plugin cannot derive license id, expiry, grace, revision, binding detail or the
reason for the derived state from the runtime mode.

Host support diagnostics export, `.dobbylicense` import and support/recovery UI
remain available in every row of this table.

## Cross-references

- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — controlling decision
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — `PluginRuntimeMode`
- `docs/schemas/dobby-plugin-license-v3.schema.json` — artifact schema
- `docs/specs/license-import.md` — import flow, grace ceiling and typed reject vocabulary
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-009`, `FR-016`, `FR-017`, `FR-028`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-006`, `AC-020`, `AC-021`, `AC-034`
