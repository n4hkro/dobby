# Backup, Restore, And Migration

**Tarix:** 2026-04-29
**Status:** P2 spec
**Scope:** Backup artifact model, restore trust boundary, audit immutability, plugin package restore source, cross-device restore, and future app key lifecycle.

## 1. Məqsəd

Backup/Restore self-service raw DB copy deyil. Bu spec host-owned online backup/restore flow-u, cross-device restore request/package modelini və restore zamanı plugin/package trust boundary-ni saxlayır.

## 2. Core Rules

- Backup raw DB file copy deyil; backup engine SQLite online backup API və ya equivalent consistent snapshot mechanism istifadə edir.
- Restore mövcud audit tarixçəsini overwrite etmir və audit immutability-ni pozmur.
- Cross-device restore explicit request/package flow tələb edir; folder copy ilə başqa cihazda app açmaq supported deyil.
- Backup artifact raw password, protected-storage secret, vendor private key və raw license secret detail daşımır.

## 3. Plugin Restore Source

- Plugin binaries backup artifact-a kor daxil edilmir.
- Restore plugin package-ləri signed package source-dan bərpa edir.
- `plugins-registry.json` və ya equivalent trusted registry package id, version, vendor və signature metadata-sını saxlayır.
- Restore zamanı package signature və compatibility host tərəfindən yenidən yoxlanır.
- Missing/untrusted plugin package restore-u fail-closed olur; domain data plugin code olmadan arbitrary şəkildə execute edilmir.

## 4. Cross-Device Restore

- Restore request target device/context identity-ni non-secret support artifact kimi daşıyır.
- Restore package yalnız host verification-dan sonra qəbul edilir.
- Device/customer/deployment binding license lifecycle qaydalarını bypass etmir.

## Backup request and protected content rules

Protected secret backing file-lar raw şəkildə backup/restore package payload-ına yığılmamalıdır. Əgər həmin material gələcək cross-device restore üçün lazımdırsa, yalnız owning protected-storage/restore contract vasitəsilə wrapped və target-bound formada daşınmalıdır.

Shared Data Tools P2 aktivləşdirildikdə mapping profile artifacts backup scope-a yalnız host-owned storage contract vasitəsilə daxil edilir; physical path bu spec-də əvvəlcədən bağlanmır.

Cross-device restore P2/future scope-dan P1-ə çəkilmir. Həmin scope aktivləşdiriləndə cross-device restore request materialı aşağıdakı lifecycle-i saxlamalıdır:

- default expiry 24 saatdır, owning config/policy başqa qərar verməyibsə;
- Admin Tool və ya package generator expired request əsasında restore package yaratmır;
- generated restore package expiry-si source request `expiresAt` dəyərindən götürülür;
- eyni anda yalnız 1 aktiv restore request saxlanılır; yeni request köhnə request materialını əvəz edir;
- successful consume-dan sonra request materialı delete/invalidate edilir və təkrar istifadə edilə bilməz.

## 5. App Key Lifecycle

Future restore/migration key materialı login-first SLA-nı bloklamır. Local app-side asymmetric key generation/rotation lazımdırsa:

- login UI first paint-dən əvvəl heavy key generation başlamır;
- key generation first-paint sonrası və ya explicit restore/recovery action zamanı lazy/on-demand işləyir;
- key source və protected-storage boundary `ADR-0055` və storage spec ilə uyğun qalır;
- key lifecycle raw key materialı diagnostics/audit export-a daxil etmir.

## 6. Settings Surface

Settings/About yalnız backup/restore status, last backup metadata və support-facing safe metadata göstərə bilər. Raw artifact content, secret key materialı və raw license detail göstərilmir.

## 7. References

- `docs/02_ARCHITECTURE_MAP.md` Phase 2 / Phase 3 / P1-if-time Future Scope row `Backup / restore / migration`
- `FR-020`, `NFR-018`
- `docs/specs/settings-and-about.md`
- `docs/specs/plugins-manager.md`
- `docs/specs/storage-and-data-protection.md`
- `ADR-0055`

## Branch Shared Backup / Restore Addendum

Standalone backup remains local SQLite/protected-storage oriented.

Branch Shared authoritative backup uses PostgreSQL-native backup/dump tooling
selected by `docs/specs/postgres-deployment-ops.md`. Branch Shared restore must
run post-restore schema/grant validation before the branch is considered
healthy.

Restore-to-production preserves `branchDeploymentId`. Restore-to-test/staging
requires explicit BranchSetup reassignment so cloned environments do not collide
with production identity.

Owner recovery password flow remains consistent with `ADR-0039` and `ADR-0055`.
BranchSetup does not auto-generate `.dobbyrecoveryrequest` and does not convert
setup proof into recovery proof.

Main-device failure recovery is Tier B/customer-release runbook scope. It is not
a Mərhələ 1 skeleton or docs-only implementation requirement beyond being
captured in `docs/specs/postgres-deployment-ops.md`.
