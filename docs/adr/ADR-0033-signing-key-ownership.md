# ADR-0033: Signing Key Ownership

**Status:** Accepted
**Date:** 2026-04-24

## Context

`ADR-0032` plugin signature trust modeli üçün host binary-ya embed olunmuş vendor master **public key**-i tələb edir. `ADR-0039` Owner recovery artefakt imzalanması üçün eyni master key-i paylaşır. Nəticədə host-da tək vendor signing key surface-i mövcuddur və onun **private** hissəsinin mülkiyyəti, saxlanması və provisioning-i bu ADR-in öhdəliyindədir.

`02_ARCHITECTURE_MAP.md` Cədvəl 3 `Signing Key Ownership` sətri bu məsələni `Phase 1 deferred decision` kimi göstərirdi; audit MF-04 tapıntısı göstərdi ki, yeni müştəri deployment-ı üçün vendor public key-in host binary-yə necə daxil olması və vendor identity provisioning prosesi heç bir sənəddə tərif olunmayıb. Bu ADR həmin boşluğu minimum scope-da bağlayır.

## Decision

Phase 1 signing key ownership və provisioning modeli:

### 1. Tək master key

- Phase 1-də vendor üçün **tək** ECDSA P-256 master signing key mövcuddur.
- Private key yalnız vendor tərəfdə mövcud olur; heç bir müştəri cihazına, host binary-yə və ya installer-ə daxil edilmir.
- Public key host binary build-time resource kimi embed olunur (`Dobby.Security / Plugin Trust Verification` və `Dobby.Security / Authentication / Session` namespace-lərinin internal resource-u).

### 2. Private key saxlanması (vendor side)

- Phase 1-də private key vendor tərəfdə offline, air-gapped storage-da saxlanır (məs. encrypted USB və ya offline HSM).
- Private key üçün backup copy minimum iki fərqli fiziki location-da saxlanır.
- Private key ilə imza yaratmaq yalnız `Dobby.Admin / Owner Recovery Artefakt Generation` (Row 86) və plugin packaging CLI vasitəsilə baş verir.
- Private key ilə birbaşa əl işi (cat, echo, export) qadağandır; yalnız ADR-0032/ADR-0039 imza flow-ları vasitəsilə istifadə olunur.
- Dev/QA sample plugin və smoke license artifact-ları production vendor private
  key ilə imzalanmır. Sample verification üçün non-production signing profile
  istifadə olunur və həmin profile customer production trust/key custody
  prosesi ilə qarışdırılmır.

### 2a. Dev/QA Host Trust Profile

Dev/QA sample plugin və smoke license artifact-ları build-time dev/QA trust profile
ilə yoxlanılır. Bu profile yalnız debug/test host build-lərində aktiv ola bilər və
runtime config ilə customer artifact-da açıla bilməz.

Canonical vendor identity qaydası:

- production artifact-lar production vendor identity-si və production master public key
  ilə yoxlanılır;
- dev/QA sample artifact-lar `vendorId = "dobby.dev-qa"` daşıyır və yalnız dev/QA
  trust profile-də tanınan non-production public key ilə yoxlanılır;
- production host build `dobby.dev-qa` vendor id-sini tanımır və belə artifact-ları
  fail-closed reject edir;
- dev/QA artifact-lar production master private key ilə imzalanmır.

Customer MSIX/portable artifact-da dev/QA public key, dev/QA trust profile metadata,
dev sample `.dobbyplugin` və ya dev sample `.dobbylicense` default şəkildə tapılarsa
release packaging verification fail etməlidir.

Dev/QA public key bytes `src/Dobby.Host/Trust/dev-qa-public-key.pem` path-ində
canonical build-time resource kimi saxlanılır və yalnız dev/QA trust profile üçün
commit edilir. Dev/QA private signing material repository daxilində saxlanmır,
`.gitignore` ilə qorunur və production master private key ilə qarışdırılmır.
`docs/specs/admin-tool.md` içindəki `tools/sign-plugin-package/` və
`tools/create-plugin-license/` scripted operator wrappers tələb olunan dev/QA
private material yoxdursa actionable stop ilə dayanmalıdır; bu ADR həmin wrapper
process-i yenidən tərif etmir.

### 3. Public key provisioning (host build side)

- Host binary build pipeline vendor master public key-i `src/Dobby.Host/Trust/vendor-master-public-key.pem` resource kimi oxuyur və compile-time embed edir (`.csproj` resource inclusion).
- Dev/QA build-time trust profile non-production public key-i `src/Dobby.Host/Trust/dev-qa-public-key.pem` resource path-i ilə istifadə edir, amma customer production build-də həmin resource include edilmir.
- Public key fayl build artifact-da istifadə olunur; runtime-da dynamic yükləmə yoxdur.
- Yeni müştəri deployment üçün public key-i dəyişdirmək Phase 1-də mümkün deyil — bütün müştərilər eyni vendor public key ilə build olunmuş host binary-ni işlədir.

### 4. Rotation scope

- Phase 1-də key rotation protocol mövcud deyil.
- `keyVersion` field (`ADR-0039 § 4`, plugin signature manifest-i və `.dobbylicense`
  signature/payload metadata-sı) forward-compatibility üçün saxlanılır. Bu field
  bütün artifact formatlarında JSON number-dır: Phase 1-də həmişə `1` dəyərini
  daşıyır, gələcək rotation dəyərləri də `"v2"`/`"v3"` string label-i kimi yox,
  numeric `2`, `3`, ... kimi artırılır.
- Key rotation tələb olunarsa yeni host binary build edilir və müştərilərə update-i çatdırılır (manual replacement Phase 1-də, host update automation P2 deferred host update strategy ilə bağlanır).
- ADR-0042 hardware baseline qərarıdır və signing-key rotation protocol-unun owner-i deyil. Rotation support implementasiya olunmadan əvvəl gələcək signing-key-rotation ADR-i və ya bu ADR-ə explicit amendment tələb olunur.

### 5. Compromise response

- Private key compromise aşkar olarsa vendor yeni master key çıxarır, yeni host build edir və bütün müştəriləri qovşağı dəyişdirmələri üçün xəbərdar edir (out-of-band kanal).
- Phase 1-də compromised key-in revoke mexanizmi yoxdur; müştəri yeni host build tətbiq etməyənədək compromised key istifadədə qalır.
- Bu qısıtma Phase 1 skeleton məqsədinə uyğundur; enterprise-grade revocation mechanism (CRL/OCSP + online check) offline-first prinsipi ilə uzlaşmır və P2-dən sonra yalnız opsional capability kimi qiymətləndirilir.

### 6. Rejected alternatives

- **Per-customer signing key:** rədd edildi — Phase 1-də deployment sadəliyi və operational cost minimumu üçün single master key seçilir; per-customer trust P2-də açıla bilər.
- **Hardware HSM məcburiyyəti:** Phase 1-də rədd edildi — solo dev / small team konteksti; offline air-gapped storage yetərlidir.
- **Dual key rotation Phase 1-də:** rədd edildi — rotation protocol skeleton-dan kənarda saxlanır; `keyVersion` field forward-compatibility saxlayır.

## Consequences

- Plugin packaging və recovery artefakt generation eyni vendor master key-ə söykənir.
- Yeni host binary build yalnız vendor tərəfdə mümkündür; müştəri öz build-ini edə bilmir (trust anchor müştəri-controlled olmur).
- Key rotation Phase 1-də kəsintili və manual prosesdir; P2-də automation açılır.
- Vendor identity provisioning (yeni müştəri deployment): vendor public key host binary ilə birlikdə gəlir; əlavə provisioning addımı lazım deyil.
- Private key security responsibility vendor tərəfdədir; bu ADR yalnız minimum standart tərif edir, operational detail-lər vendor internal səviyyəsində saxlanır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 38 `Plugin Trust Verification`, Row 86 `Owner Recovery Artefakt Generation`, Cədvəl 3 `Signing Key Ownership` risk sətri
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — public side signature verification flow
- `docs/adr/ADR-0039-owner-password-recovery.md` — recovery artefakt imzalama flow
- `docs/02_ARCHITECTURE_MAP.md` — Row 82-83 `Dobby.Update` P2 update boundary (deferred)
- `docs/specs/admin-tool.md` — P1 minimal scripted operator artifact path
- `docs/specs/sample-plugin.md` — dev/QA verification artifact boundary
