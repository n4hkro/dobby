# License v3 Derived Lifecycle Truth Table

The signed authority contains only `granted` or `revoked` intent. Runtime state
is never accepted from the artifact and is derived on every evaluation.

Rules are evaluated from top to bottom:

| Intent / validation | Trusted time | Evaluation instant | Derived state |
|---|---|---|---|
| malformed, bad signature, bad purpose/key, binding failure or anti-rollback failure | any | any | Denied |
| revoked | any | any | Denied |
| granted | unhealthy or rollback detected | any | any | Denied |
| granted | healthy | `now < notBefore` | Pending |
| granted | healthy | `notBefore <= now <= expiresAt` | Active |
| granted | healthy | `expiresAt < now <= checked(expiresAt + gracePeriodDays)` | Grace |
| granted | healthy | `now > checked(expiresAt + gracePeriodDays)` | ReadOnlyExpired |

Timestamp ordering requires `issuedAt <= notBefore < expiresAt`. Grace addition
uses checked arithmetic; overflow is malformed and therefore Denied. Boundary
comparisons are exact UTC instants. An imported v2 `status` never overrides this
table and cannot create a new v3 grant.
