# Dobby Specs Index

**Status:** Accepted index
**Date:** 2026-05-06

This file is the narrow specs index chosen by `HD-007` / `DM-094`. It replaces
the need for a global `docs/DOCS_STRUCTURE.md` file for the current
storage/deployment close-out.

## Ownership Table

| Spec | Scope / canonical ownership |
|---|---|
| `admin-tool.md` | Vendor-side Admin Tool, signing/recovery/license artifact generation, and boundary from customer-side BranchSetup. |
| `audit-and-diagnostics.md` | Audit taxonomy, diagnostics boundary, redaction, Branch Shared audit origin payload, and no-telemetry default. |
| `backup-restore-migration.md` | Backup/restore/migration boundaries, cross-device restore rules, and Branch Shared restore identity handling. |
| `data-retention.md` | Audit/diagnostics retention floors, security-state (throttle, consumed nonce, key material) retention rules, and future-scope policy extensions. |
| `design-system-assets.md` | Typography/spacing/radius/motion/color token inventory, shell measurement tokens, reduced-motion və high-contrast accessibility policy, asset naming və app/plugin icon resource rules. |
| `device-lifecycle.md` | Device identity boundary, recovery/support-safe identifier semantics, Branch Shared registration, suspend/decommission, replacement, and support-safe metadata. |
| `license-import.md` | `.dobbylicense` import flow, permission guard, non-disclosing license status, and `customerDeploymentId` prose-only interpretation. |
| `onboarding-and-provisioning.md` | BranchSetup setup proof, first Owner provisioning, device registration, and setup evidence routing. |
| `plugins-manager.md` | Plugin install/enable/disable/uninstall behavior, runtime fault isolation, and plugin storage mode boundary. |
| `postgres-deployment-ops.md` | Branch Shared PostgreSQL setup artifact/runbook, grants, operational protection, backup/restore evidence, and Tier B release readiness. |
| `sample-plugin.md` | Sample/test plugin verification artifact and platform acceptance coverage. |
| `session-lifecycle.md` | Login/logout/lock/session invalidation behavior, remembered-login (no-secret) semantics, durable login/unlock throttling, Owner Recovery v2 artifact flow, and Branch Shared session availability semantics. |
| `settings-and-about.md` | Settings/About surface, privacy-safe support fields, Account/Security/User Management entries. |
| `shared-data-tools.md` | P2 shared import/export mapping tools and mapping-profile storage boundary. |
| `shell-ui.md` | Shell layout, navigation surfaces, keyboard/accessibility baseline, and Branch Shared shell status rendering boundary. |
| `storage-and-data-protection.md` | Storage taxonomy, DeviceLocal/BranchShared protection, plugin storage compartments, and `branchshared.json` schema ownership. |
| `user-management.md` | User/permission management, Owner guard, password change/reset proof boundary, first Owner mode boundary, and Branch Shared user authority. |
| `work-sessions.md` | Process-lifetime work session minimize/restore behavior, work-session owner isolation (session id is not a capability), bounded work-session resource limits, deterministic closure, and Branch Shared availability continuity boundary. |

## Governance

- This index is descriptive; each spec remains canonical for its own behavior.
- Cross-document skeleton naming and wire/storage terminology are centralized in
  `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md`.
- JSON Schema `$id` values are reserved by `docs/schemas/*` files. Specs may
  describe schema ownership and validation behavior, but must not introduce a
  second `$id` authority or prose-only replacement for a schema `$id`.
- `Cədvəl N` references are local to their owning document unless the reference
  names an explicit document path. Cross-doc references should prefer document
  path + section/row title over bare table numbers.
- Adding a new spec requires adding one row here with its ownership boundary.
- Skeleton-critical spec ownership additions should extend the table above rather
  than creating a second ownership index; this table is the durable specs index
  chosen by `HD-007` / `DM-094`.
- This file does not authorize code skeleton work. Implementation phases remain
  governed by the accepted ADR/spec set and explicit human approval.

## Authority Note

- Konflikt həlli sırası: (1) ən yeni applicable Accepted ADR — xüsusilə 2026-08 hardening seriyası `ADR-0058`
  … `ADR-0063` və sonrakılar; (2) owning schema (`docs/schemas/`); (3) owning executable spec
  (bu index-dəki fayllar); (4) canonical `docs/01`–`docs/09`; (5) cari
  implementation plan — **bu tier hazırda boşdur**, çünki
  `docs/implementation-plan/` ağacı (master execution plan, traceability/findings,
  `status-register.json`) `a8a5914` və `35148c2` commit-ləri ilə retire edilib;
  (6) tarixi plan/evidence.
- Bu index-dəki spec-lərin normativ məzmunu **body mətnindədir**. Ayrıca
  "controlling amendment" bölməsi saxlanmır: 2026-08 hardening qərarları müvafiq
  bölmələrə köçürülüb və izi yalnız hər spec-in `Revision History` bölməsində
  qalır. Stale mətnin üzərinə note qoymaq yolverilməzdir — mətn birbaşa düzəlir.
- Tarixi sprint/implementation planları evidence-dir, cari authority deyil.
- Yaşıl test və mövcud kod product decision üçün avtomatik authority deyil; test
  yalnız spec-in tələb etdiyi davranışın **sübutudur**. Uyğunsuzluq halında
  controlling ADR/spec qalib gəlir və kod/test onlara uyğunlaşdırılır.
- Runtime, security və release gate-ləri yalnız behavior-first sübutla qapanır
  (`NFR-027`, `ADR-0061`). "Source-da termin var", regex/static scan nəticəsi,
  fayl mövcudluğu, package reference və "test list green" iddiası bu gate-ləri
  qapatmır. Gate machine-local publish output-una deyil, current build-ə qarşı
  işləməlidir.
- Bu repository-də ayrıca `docs/00_*` authority baseline faylı saxlanmır: authority
  sırası bu bölmədədir, document ownership sərhədləri isə
  `docs/adr/README.md § Documentation Governance`-dədir. Retired plan və audit
  baseline faylları geri qaytarılmır (`HD-007` / `DM-094` narrow-index qərarı).

## Revision History

- **2026-08-27:** Ownership sətirləri düzəldildi: `design-system-assets.md` sətri
  "design asset inventory and visual baseline references" kimi göstərilirdi, halbuki
  həmin spec typography/spacing/radius/motion/color token-larının, shell measurement
  token-larının və reduced-motion/high-contrast policy-sinin canonical owner-idir;
  `work-sessions.md` sətrinə owner isolation, bounded resource limits və deterministic
  closure ownership-i əlavə olundu.
- **2026-08-26:** Authority Note-a `ADR-0062` daxil edildi, behavior-first gate
  qaydası (`NFR-027`) və retired `docs/00_*` baseline-ın geri qaytarılmaması
  bəndi explicit yazıldı.
- **2026-08-26:** Ownership sətirləri `ADR-0058`…`ADR-0061` reconciliation-ından
  sonra yeniləndi (`session-lifecycle`, `user-management`, `device-lifecycle`,
  `data-retention`); `2026-08 Authority Note` amendment-pointer mətni konflikt
  sırası ilə əvəz olundu.
- **2026-05-06:** `HD-007` / `DM-094` ilə qəbul edilmiş ilk narrow specs index.
