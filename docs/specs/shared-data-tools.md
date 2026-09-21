# Shared Data Tools

**Tarix:** 2026-04-29
**Status:** P2 spec
**Scope:** Host-owned shared import/export engine, Excel/file mapping workflow, mapping profile storage, validation orchestration, and plugin identity binding.

## 1. Məqsəd

Shared Data Tools plugin-lərin ayrı-ayrı təkrar yazmamalı olduğu import/export və mapping workflow-larını host-owned shared capability kimi saxlayır. Bu spec Excel/file import, header normalization, mapping profile, validation orchestration və export writer davranışını canonical edir.

## 2. Host-Owned Responsibilities

- File selection/picker boundary host adapter-lərindən keçir.
- Header normalization case, whitespace, punctuation və localized label fərqlərini canonical field matching üçün normalize edir.
- Alias/fuzzy matching yalnız suggestion verir; final mapping user və ya plugin-declared profile tərəfindən təsdiqlənir.
- Manual mapping UI host-owned surface-dir və plugin arbitrary mapping UI inject etmir.
- Validation orchestration plugin-owned field validator-ları çağırır, amma import transaction və result aggregation host flow-da qalır.
- Export writer format-specific escaping/encoding və file output boundary-ni saxlayır.

## 3. Plugin-Owned Responsibilities

- Plugin canonical field modelini, field labels, supported import/export operations və validation rules təqdim edir.
- Plugin domain transformation və business validation plugin responsibility-dir.
- Plugin caller identity-ni input kimi diktə etmir.

## 4. Mapping Profile Storage

Mapping profile key-i host tərəfindən aşağıdakı tuple ilə saxlanır:

```text
callerPluginId + profileKey + canonicalFieldVersion
```

- `callerPluginId` authenticated plugin context-dən çıxır.
- `profileKey` plugin-declared stable key-dir.
- `canonicalFieldVersion` plugin field modelinin compatibility version-ıdır.
- Plugin-supplied input `callerPluginId` kimi qəbul edilmir.
- Field-level mapping schema bu spec-in içində versioned model kimi saxlanır və storage provider detail-ları plugin-ə açılmır.

## Mapping profile audit and diagnostics visibility

Shared Data Tools P2 aktivləşdirildikdə mapping profile create/update/delete və mapping-related import/export profile dəyişiklikləri host-owned audit/diagnostics visibility almalıdır.

Audit event-lər profile identity-ni support-safe formada göstərə bilər, amma raw imported file content, raw local path, protected secret material və plugin-private business payload daşımamalıdır. Diagnostics detail `docs/specs/audit-and-diagnostics.md` redaction boundary-sinə tabedir.

Physical storage path bu qayda ilə bağlanmır; source-of-truth yenə host-owned storage contract-dır.

## 5. Runtime Mode

Read-only expired mode, missing permission və disabled plugin state import/export flow-larını fail-closed bloklayır. Shared Data Tools host permission/runtime mode qaydalarını bypass etmir.

## 6. References

- `docs/02_ARCHITECTURE_MAP.md` Row 61, Row 62
- `docs/02_ARCHITECTURE_MAP.md` Future Scope row `Shared Data Tools`
- `docs/02_ARCHITECTURE_MAP.md` Engineering Conventions / Boundary Rules `Shared Mapping Profile Sərhədi`
- `docs/specs/storage-and-data-protection.md`

## Branch Shared Scope Note

Shared Data Tools remain P2/future. Branch Shared mode does not pull them into
Phase 1 and does not create cross-branch or OrganizationShared data movement.

If this spec later opens for Branch Shared, mapping profile storage must still
use host-owned storage contracts and must not expose physical PostgreSQL/SQLite
provider details to plugins.

## Revision History

- **2026-08-27:** Revision History bölməsi əlavə olundu (spec metadata
  konsistentliyi; bu spec-in P2 scope-u dəyişməyib). Bu tarixdən əvvəlki
  dəyişikliklər git tarixçəsində izlənir.
