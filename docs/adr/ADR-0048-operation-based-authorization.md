# ADR-0048: Operation-Based Authorization

**Status:** Accepted
**Date:** 2026-04-25

## Context

`Owner` user management və permission foundation mövcuddur, amma operation-level authorization shape-i formal bağlanmamışdı. `ADR-0004` capability enforcement model-i typed `ICapabilityPolicy<TCapability>` pattern-i tələb edir və god-interface/string-only enforcement-i rədd edir.

Owner qərarı permission-ların bölmə siyahısı kimi əllə saxlanmamasını tələb edir. Dobby-dəki built-in bölmələr və plugin-lərin elan etdiyi bölmələr host-owned permission registry-yə düşməli, həmin target-lər isə yalnız canonical permission verb-lərdən dəstəklədiklərini göstərməlidir. Model grant-only/on-off olmalıdır; explicit deny permission record-u və deny toggle-u yoxdur.

Bu ADR Phase 1 üçün registry-based operation authorization modelini bağlayır.

## Decision

Operation registry-nin P1 owner-i `Dobby.Security / Permissions / Policy` layer-idir.

Registry target-ləri iki yolla yaranır:

- built-in target-lər host tərəfindən qeyd olunur;
- plugin target-lər validated signed manifest `operationTargets` metadata-sından
  package validation zamanı registry-yə seed olunur.

Verb set host-defined closed set-dir. Plugin free-form verb və ya custom operation key formatı təqdim edə bilməz; plugin yalnız öz target-ləri üçün dəstəklədiyi canonical verb-ləri elan edə bilər.

Canonical permission verb-lər:

| Verb | UI termini | Məna |
|---|---|---|
| `view` | Görmək | Target-i görə və oxuya bilir. |
| `create` | Yaratmaq | Target daxilində yeni entity/data yarada bilir. |
| `update` | Dəyişmək | Mövcud data və ya target məlumatını dəyişə bilir. |
| `delete` | Silmək | Data silə, deactivate edə və ya lifecycle-dan çıxara bilir. |
| `execute` | İcra etmək | Workflow, command və proses başladır. |
| `import` | Import | Fayl, package, license və ya kənar data daxil edə bilir. |
| `export` | Export | Data, report, backup və ya request faylı çıxara bilir. |
| `print` | Çap etmək | Physical/allowed print target-ə çıxış edə bilir. |
| `configure` | Konfiqurasiya | Target ayarını, davranışını və ya lifecycle state-ini dəyişə bilir. |
| `grant` | İcazə vermək | Həmin target üzrə başqa managed user-lərə icazə verə və ya söndürə bilir. |

Capability payload:

```csharp
public readonly record struct OperationPermissionCapability(string OperationKey);
```

Evaluation:

```text
OperationPermissionCapability -> ICapabilityPolicy<OperationPermissionCapability> -> PolicyDecision
```

Operation key naming convention:

- operation key forması `<targetId>.<verb>`-dir;
- `<verb>` yalnız yuxarıdakı canonical siyahıdan seçilir;
- `<targetId>` registry-də olan built-in və ya plugin target id-sidir;
- operation key parser-i son nöqtədən (`.`) split edir: suffix canonical
  `verb`, prefix isə `targetId` kimi interpret olunur. Target id-lər nöqtə
  daşıya bildiyi üçün first-dot split və ya segment-count assumption qəbul
  edilmir.
- hər target dəstəklədiyi verb-ləri elan edir; dəstəklənməyən verb UI-da toggle kimi göstərilmir.

Unknown target və ya unknown verb fail-closed deny edilir və audit warning yaradır.

### Plugin operation target source

`HD-009` / `DM-096` üzrə P1-də plugin permission target-lərinin canonical source-u
signed manifest metadata-dır:

```json
{
  "operationTargets": [
    {
      "id": "com.dobby.sample.greet",
      "verbs": ["execute"],
      "labelKey": "sample.greet.label"
    }
  ]
}
```

Manifest validation `ADR-0036` qaydası ilə plugin assembly load və activation-dan
əvvəl işləyir. Host registry yalnız bu validated metadata-dan yaranan plugin
target-ləri qəbul edir; runtime plugin code və SDK attribute discovery P1 source
deyil.

Rules:

- operation key forması `<targetId>.<verb>` olaraq qalır;
- parser son nöqtədən (`.`) split edir; `com.dobby.sample.greet.execute`
  operation key-i `targetId = com.dobby.sample.greet` və `verb = execute`
  kimi oxunur;
- `targetId` manifest `operationTargets[].id` dəyəridir;
- `verb` həmin target-in `operationTargets[].verbs` siyahısında olmalıdır;
- unknown target, unknown verb və target-in elan etmədiyi verb fail-closed deny
  edilir;
- manifest-də unknown verb, empty verbs, duplicate target id, invalid target id
  və missing `labelKey` package validation failure-dır.

SDK attribute discovery P1-də rədd edilir, çünki attribute oxumaq plugin
assembly load tələb edir və validation-before-activation trust boundary-ni
zəiflədir. Future SDK helper/analyzer manifest metadata generation üçün
düşünülə bilər, amma canonical source yenə signed manifest qalır.

Reference mapping:

| Klassik əməliyyat | Canonical verb |
|---|---|
| Bölməni açmaq / history görmək | `view` |
| Entity/data yaratmaq | `create` |
| Məlumat redaktəsi | `update` |
| Silmək, deactivate, uninstall | `delete` |
| Workflow, reset password, proses başlatmaq | `execute` |
| Plugin install | `import` |
| Plugin update/repair | `import` |
| License import | `import` |
| Backup, recovery request, report çıxarmaq | `export` |
| Print/PDF çıxışı | `print` |
| Plugin enable/disable | `configure` |
| Bölmə/plugin ayarlarını dəyişmək | `configure` |
| Başqasına icazə vermək | `grant` |

Built-in User Management target-i host tərəfindən registry-yə yazılır və minimum bu
mapping ilə işləyir:

| User Management əməliyyatı | Canonical verb |
|---|---|
| User siyahısını və user detail-ini görmək | `view` |
| Managed user yaratmaq | `create` |
| Managed user məlumatını redaktə etmək | `update` |
| Managed user deactivate/reactivate etmək | `delete` |
| Managed user password reset etmək | `execute` |
| Managed user-ə permission vermək və ya söndürmək | `grant` |

Built-in License target-i host tərəfindən `plugins.license` target id-si ilə registry-yə
yazılır. Minimum mapping:

| License əməliyyatı | Canonical verb |
|---|---|
| Plugin license statusunu görmək | `view` |
| `.dobbylicense` import etmək | `import` |

Built-in Plugins Manager target-i host tərəfindən `plugins.management` target id-si ilə
registry-yə yazılır. Minimum mapping:

| Plugins Manager əməliyyatı | Canonical verb |
|---|---|
| Plugin siyahısını və detail-ini görmək | `view` |
| `.dobbyplugin` install etmək | `import` |
| Plugin enable/disable etmək | `configure` |
| Plugin uninstall etmək | `delete` |
| Plugin update/repair etmək | `import` (P2) |

Authorization behavior:

- `Owner` bootstrap zamanı all-grants semantics daşıyır.
- `Owner` protected principal-dır; managed user `Owner` məlumatına, statusuna və permission-larına toxuna bilməz.
- Managed user default-deny modelindədir: grant yoxdursa operation icra edə bilmir.
- Explicit deny permission record-u və deny toggle-u yoxdur.
- Managed user özünə permission əlavə edə bilməz.
- Managed user özündə olmayan permission-u başqasına grant edə bilməz.
- Managed user `Owner`-i deactivate/reactivate edə, `Owner` password reset edə və ya
  Owner transfer/replacement aça bilməz; bu self-service User Management edit deyil.
- Managed user password reset User Management əməliyyatıdır, Owner recovery flow deyil.
- Deny decision audit trail-ə yazılır.
- User/plugin-facing deny reason non-disclosing qalır.

## Consequences

- Permission model operation-level test edilə bilən hala gəlir.
- `ADR-0004` typed capability pattern-i qorunur.
- Operation key string-ləri storage/UI vocabulary-dir; enforcement contract forması typed capability-dir.
- Target registry extensible qalır, amma verb vocabulary P1-də qapalıdır.
- Plugin signed manifest `operationTargets` target contribution verə bilər, lakin arbitrary verb və ya host policy bypass edə bilməz.
- Permission UI bütün Dobby bölmələrini əllə sadalamır; registry-dəki target və supported verb-lərdən render olunur.
- UI visibility authorization deyil: command gizlətmək və ya disable etmək UX convenience-dir; hər protected operation service/operation boundary-də policy ilə yoxlanır.
- User Management öz canonical target-i ilə permission modelinə qoşulur; ayrıca
  hardcoded admin shortcut permission bypass yarada bilməz.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — İstifadəçi Modeli
- `docs/02_ARCHITECTURE_MAP.md` — Row 37 `Permissions / Policy`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-003`, `FR-018`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-017`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-022`, `AC-030`
- `docs/specs/user-management.md`
- `docs/specs/plugins-manager.md`
- `docs/specs/license-import.md`
- `docs/adr/ADR-0004-capability-enforcement-model.md`
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md`

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-009` | RESOLVED_BY_HUMAN_DECISION (`DM-096`): plugin permission targets are declared by signed manifest `operationTargets = [{ id, verbs, labelKey }]`; package validation owns target shape before plugin assembly load. |

## Reopen Conditions

These resolved decisions must not be reopened for preference-only reasons.

| Decision | Reopen condition |
|---|---|
| `DM-096` / `HD-009` | A required plugin needs dynamic operation targets that cannot be known at package-validation time. |

## Branch Shared Authorization Amendment

Branch Shared PostgreSQL grants are an operational least-privilege floor, not a
replacement for application-layer authorization. Dobby must still enforce
operation permissions at application/service boundaries.

Direct database mutation by an operator bypasses Dobby authorization, audit
semantics and domain invariants. This is an operational risk documented by
`docs/specs/device-lifecycle.md` and `docs/specs/postgres-deployment-ops.md`;
Dobby does not pretend such changes are application-authorized.
