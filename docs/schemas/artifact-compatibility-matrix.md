# Artifact Compatibility Matrix

**Tarix:** 2026-08-27
**Status:** Accepted
**Sources:** ADR-0059, ADR-0060, ADR-0062, ADR-0063, NFR-004, NFR-027, AC-049,
docs/specs/license-import.md, docs/specs/plugins-manager.md,
docs/specs/session-lifecycle.md

This matrix freezes the transition policy for versioned Dobby artifacts. Schema
validation never implies authorization, installation or activation.

## 1. Terms

- **Successor input** — the only artifact version a P1 runtime path accepts.
- **Legacy input** — a previously specified version. Being listed here is *not* a
  statement that a reader for it exists.
- **Activation gate** — the point at which the successor version became the only
  accepted version for a given artifact family. For every row below that gate is
  already closed in P1: the successor is the sole accepted version at HEAD, so
  there is no open transition period to manage.

## 2. Matrix

| Artifact | Legacy input | Successor input | P1 runtime read path | New import/install | Migration decision |
|---|---|---|---|---|---|
| Plugin manifest | v1 | v2 | None. Compatibility evaluation rejects `schemaVersion != 2` with `unsupported-manifest-version`, at install validation and again at activation re-verification. A previously installed plugin's inventory row is rendered from persisted install metadata, not by re-reading a v1 manifest. | v2 only | Reverify and repackage as v2; never rewrite a signature in place |
| Plugin license | v2 | v3 | None. The signature block must carry exactly `purpose`, `algorithm`, `format`, `keyId`, `value`, so a block bearing the retired numeric `keyVersion` is a shape violation rejected before signature verification. `schemaVersion != 3` and any non-v3 payload shape are then rejected as `invalid-license-file` before any state change; no v2 `status` value is normalized to a runtime mode. | v3 only | Reissue as a signed v3 revision; a v2 file never becomes a grant and never extends one |
| Owner recovery | v1 hardware-threshold request/response | v2 device-key-bound request/response | None. Request verification and response import both reject `schemaVersion != 2`; the exporter emits v2 only. | v2 only | Reject v1 authorization; issue a new v2 request after device-key provisioning |
| Branch setup | none; `.dobbylicense` is not setup authority | setup authorization v1 | None by design; Branch Shared runtime is deferred. Schema validation/design review only. | no Standalone runtime handler | Deferred to Branch Shared by design |

Unknown schema versions, unknown purposes, malformed values and cross-protocol
artifacts are rejected. There is no silent fallback between rows, and no row
carries a "try the older reader" path.

### 2.1 Why the frozen v1 manifest schema is still shipped

`Dobby.PluginFramework` embeds both `dobby-plugin-manifest.schema.json` (v1) and
`dobby-plugin-manifest-v2.schema.json`, but only the v2 resource is ever loaded.
The v1 copy is a **frozen negative reference**, not a read path:

- The two shapes are mutually exclusive. v1 sets `additionalProperties: false` and
  does not declare `schemaVersion`, `purpose` or `hostApiVersion`, so a canonical v2
  manifest is invalid under v1 and a v1 manifest is invalid under v2.
- That mutual exclusivity is what makes the enforced-schema check behavioral rather
  than a source-text assertion: running the validator over the canonical v2 fixture
  and over v1-shaped fixtures shows which schema is actually enforced.
- Keeping the frozen file byte-identical to its docs copy also keeps the historical
  contract auditable without granting it any authority.

Shipping it must not be justified as "legacy inventory read". No inventory,
diagnostics, export or support path parses a v1 manifest.

## 3. Opening a legacy reader later

`ADR-0060` ships **no** v2 reader in P1 and permits a later one only through an
"explicit, separately approved compatibility path ... covered by fixed retirement
dates and negative fixtures". A reader may therefore be added only when all of the
following are satisfied first:

1. The owning executable spec declares a **concrete** retirement bound — a date or
   a host version — not the word "bounded".
2. This matrix row is updated in the same change, so the runtime read path column
   stops saying "None".
3. Negative fixtures prove the legacy input cannot create a new grant, extend an
   existing one, escalate a runtime mode, or downgrade the successor version.
4. The reader normalizes into the successor internal model only; the legacy
   vocabulary never reaches a persisted or plugin-visible surface.

A compatibility reader without a declared retirement bound is prohibited: it
reopens exactly the downgrade surface `ADR-0060` and `ADR-0059` closed.

## 4. Verification Obligations

Behavior-first proof is required (`NFR-027`, `ADR-0061`). Schema-file existence,
prose and source-text scans do not close these rows:

| Yoxlama | Minimum sübut |
|---|---|
| Manifest v1 fail-closed | Correctly signed v1-schema package fixture rejected with `unsupported-manifest-version` at install validation and at activation re-verification, with no assembly load |
| Enforced manifest schema identity | Validator run over the canonical v2 fixture (accepted) and over fixtures omitting `schemaVersion` / `purpose` / `hostApiVersion` (rejected) — resource presence or a resource-name constant is not sufficient, because both schemas are embedded |
| License v2 fail-closed | Correctly signed `schemaVersion = 2` and v2-shaped (`status` present, `intent` absent) fixtures rejected with `invalid-license-file`, leaving the existing v3 last-known-good grant unchanged |
| Retired selector fail-closed | An otherwise valid artifact whose signature block adds numeric `keyVersion` (and one that replaces `keyId` with it) rejected as a signature-shape violation — the reject must occur without the retired field ever selecting a key, and a source-text search for `keyVersion` does not close this row |
| Vendor selector namespace fail-closed | Per artifact family, a committed negative fixture whose `keyId` names another family's ring (`license-prod-…` in a plugin package, `plugin-prod-…` in a license, …) and one whose selector drops the `<YYYY-MM>` month are rejected by the owning schema **at that `keyId` instance location**, before signature verification. `purpose-short` is bijective with the signing purpose (`ADR-0062` § 1), so a cross-ring selector is key-purpose confusion caught by shape; the pattern is generic over the date, so ordinary rotation needs no schema change |
| Device selector binding | A recovery request whose `possessionProof.keyId` is not `device-` + the first 16 hex characters of the signed `payload.recoveryPublicKeyHash` is rejected by request verification, which recomputes the value instead of trusting it; a vendor-formatted selector in that slot is additionally rejected by the owning schema's disjoint `device-` namespace |
| Conditional label source | A manifest contribution with no label source — non-`top-toolbar` target, or any child carrying `parentId` — and no `labelKey` is rejected by the owning v2 schema conditional and by the typed validator; only a top-level `top-toolbar` contribution may fall back to root `displayName` |
| Recovery v1 fail-closed | v1 request/response fixtures rejected by both request verification and response import; no owner credential mutation |
| No cross-protocol reuse | An artifact of one family presented to another family's reader is rejected on `purpose`/shape before signature acceptance |

## Cross-references

- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md`
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md`
- `docs/specs/license-import.md` — license v3 authority and v2 fail-closed rule
- `docs/specs/plugins-manager.md` — manifest v2 install/activation verification
- `docs/specs/session-lifecycle.md` — owner recovery v2 request/response wire contract

## Revision History

- **2026-08-29:** `Vendor selector namespace fail-closed` sətri "per artifact family"
  tələb edirdi, lakin month-less selector qaydası üçün yalnız `plugin-license-v3`
  fixture-i mövcud idi; `plugin-manifest-v2`, `recovery-response-v2` və
  `branch-setup-authorization-v1` üçün `<YYYY-MM>` tələbi sırf schema pattern-i ilə
  qorunurdu və heç bir committed sübutu yox idi. Üç month-less negative fixture əlavə
  edildi (hər biri öz ring-ini saxlayır, yalnız ay hissəsini itirir, ona görə ölçdüyü
  qayda cross-ring fixture-i ilə üst-üstə düşmür) və hər biri
  `SuccessorArtifactSchemaFixtureTests`-də öz rejection location-ları ilə qeydiyyata
  alındı. Coverage guard-ı da gücləndirildi: əvvəlki ">= 2 negative fixture" sayı bu
  boşluğu ifadə edə bilmirdi, indi `Every_vendor_selector_family_pins_both_cross_ring_and_monthless_rejection`
  hər vendor-selector family-si üçün **hər iki** qaydanı adbaad tələb edir.
  **2026-08-29 düzəlişi:** `recovery-request-v2` əvvəlcə bu siyahıdan yanlış əsasla
  çıxarılmışdı ("vendor selector slotu yoxdur"). Əslində request `payload.vendorKeyId`
  daşıyır və o, response ilə **eyni** ring-pinned `$defs/keyId` pattern-inə `$ref`-lənib,
  yəni family vendor-selector qaydasına tabedir. Nəticədə request pattern-i
  boşaldılsa (məsələn generic string-ə qaytarılsa) bütün test-lər yaşıl qalırdı. Family
  siyahıya əlavə edildi və `cross-ring-keyid` / `monthless-keyid` negative fixture-ləri
  commit edildi. Disjoint `device-` possession-proof namespace-i ayrı öhdəlikdir və
  selector-swap fixture-ləri ilə örtülür — bu qaydanın yerinə keçmir. Matrix qərarı
  dəyişmir.
- **2026-08-27 (2):** Three verification obligations added after the owning schemas
  gained fail-closed shape rules that previously lived in prose only: per-family
  vendor `keyId` namespace patterns (`ADR-0062` § 1), the disjoint `device-`
  possession-proof selector namespace, and the conditional manifest label source
  (`ADR-0036`). Each row names the committed negative fixture family that discharges
  it; before this change a cross-ring or month-less selector was accepted by every
  schema and stopped only by exact ring match at verification time, and the committed
  contract fixtures themselves carried selectors no host key ring contains.
- **2026-08-27:** Added §2.1. The frozen v1 manifest schema was documented across
  `docs/03_TECH_STACK_AND_VERSIONS.md`, `ADR-0036`, `docs/specs/plugins-manager.md`
  and `docs/specs/sample-plugin.md` as embedded "for legacy inventory read", an
  inventory path that does not exist in any assembly. The justification is now
  stated correctly (frozen negative reference enabling a behavioral enforced-schema
  proof) and an `Enforced manifest schema identity` verification obligation was
  added, because with both schemas embedded a resource-name constant proves nothing
  about which one gates installation.
- **2026-08-27:** Legacy rows were reconciled with shipped behavior. The previous
  table granted read permissions that no code implements — "valid installed v2 LKG
  may be read during the bounded migration window", "v1 may be inventoried as
  legacy", "v1 may be displayed/exported for support diagnosis only" — while
  manifest, license and recovery paths all reject the legacy version fail-closed at
  HEAD. The permissive wording was a downgrade-surface risk in both directions: an
  operator could believe a legacy artifact was still honoured, and an implementer
  could believe a legacy reader was mandated. The `Inventory/read` column was
  replaced by `P1 runtime read path` stating what actually happens; the undefined
  `activation gate` term is now defined; and `ADR-0060`'s previously unfulfilled
  "fixed retirement dates" requirement is expressed as a four-condition gate on
  ever adding a legacy reader. Verification obligations and metadata/cross-reference
  sections were added. No ADR decision changed, so no successor ADR is created.
