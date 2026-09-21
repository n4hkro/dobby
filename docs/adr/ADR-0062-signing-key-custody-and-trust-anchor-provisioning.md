# ADR-0062: Signing Key Custody and Trust Anchor Provisioning

**Status:** Accepted

**Date:** 2026-08-26

**Amends:** `ADR-0032` (trust anchor identity, key-purpose separation, rotation) və
`ADR-0055` (recovery-response verification material, key-purpose separation). Hər ikisi
`Accepted` qalır və toxunulmayan bölmələri üçün authority olaraq qalır; yalnız adı çəkilən
sahələr amend olunur. `ADR-0055` üçün scope qəsdən dəyişik amender-in əhatə etdiyi sahələrdən
ayrıdır: bu ADR yalnız recovery-response trust anchor material-ına aiddir. Bu bilə-bilə
`**Supersedes:**` deyil, çünki header oxuyan reader və
`AdrSupersessionConsistencyTests` onu adı çəkilən sənədin retired olması iddiası kimi oxuyur.

**Supersedes:** `ADR-0033` (Signing Key Ownership — fully superseded; the single vendor-master-key model is replaced by the purpose-separated hierarchy below. This document carries no `vN` suffix because `ADR-0033` had none: there is no "v1" of this title for a reader to look for.)

## Context

`ADR-0033` Phase 1 üçün **tək** vendor master signing key modelini bağlamışdı:
plugin package və Owner recovery imzaları eyni private key-i paylaşırdı, public
anchor `src/Dobby.Host/Trust/vendor-master-public-key.pem` yolunda gözlənilirdi,
`keyVersion` numeric field forward-compatibility saxlayırdı və rotation protokolu
"Phase 1-də mövcud deyil" sayılırdı.

2026-08 hardening ADR-ləri bu modeli üç yerdən pozdu:

- `ADR-0059` § `Signatures, fingerprint, and keys`: plugin-package, license və
  recovery imzalanması **ayrı purpose**-lardır və distinct versioned leaf
  key/key id istifadə edir; "one interchangeable private key" qadağandır.
- `ADR-0058` § `Owner recovery v2`: vendor response **purpose-specific recovery
  signing key** ilə imzalanır.
- `ADR-0060`: `.dobbylicense` v3 artifact-ı `keyId` string daşıyır, `keyVersion`
  numeric field-i yoxdur; license leaf key plugin-package və ya recovery key kimi
  qəbul edilmir.

Repository state-i də `ADR-0033` mətnindən uzaqlaşmışdı: real trust anchor-lar
altı purpose-separated fayldır (`vendor-plugin-package`, `vendor-plugin-license`,
`vendor-recovery` və uyğun `dev-qa-*`), `src/Dobby.Host/Trust/README.md` "legacy
shared `vendor-master` və `dev-qa` trust anchor-ları dəstəklənmir" deyir, kod
`PluginTrustPublicKeyProvider` daxilində purpose-separated resource adları və
`keyId` dəyərləri (`plugin-prod-2026-01`, `license-prod-2026-01`, ...) istifadə
edir. Bundan başqa audit tapdı ki, `.gitignore` yalnız retired `vendor-master-`
və `dev-qa-public-key.pem` adlarını `*.pem` ignore qaydasından çıxarır, yəni
**build-in embed etdiyi bütün real public trust anchor-lar version control-dan
kənarda qalır**.

Nəticədə key custody, provisioning və rotation üçün yeganə Accepted authority
(`ADR-0033`) üç newer ADR ilə ziddiyyətdə idi. Bu ADR həmin authority-ni əvəz
edir.

## Decision

### 1. Purpose-separated key hierarchy

- Vendor tərəfdə offline **root** key yalnız leaf key-ləri authorize etmək üçün
  istifadə olunur; artifact imzalamır.
- Hər signing purpose üçün ayrı leaf key mövcuddur və purpose label-ları
  `ArtifactSignaturePurposes` ilə eynidir. Vendor tərəfin imzaladığı purpose-lar:
  `dobby.plugin-package`, `dobby.plugin-license`, `dobby.owner-recovery-response`
  və (Branch Shared açılanda) `dobby.branch-setup-authorization`.
  `dobby.owner-recovery-request` və `dobby.owner-recovery-request-possession`
  device-side purpose-lardır; onları host-un non-exportable device recovery
  key-i imzalayır (`ADR-0058`) və vendor key ring-inə daxil edilmir.
- Bir purpose üçün trusted key başqa purpose-un artifact-ını verify etmək üçün
  qəbul edilmir; key-purpose confusion fail-closed rədd olunur (`ADR-0059`).
- `keyId` string **vendor** key ring-i üçün canonical key selector-dur. Format
  `<purpose-short>-<prod|test>-<YYYY-MM>`-dır və `purpose-short` hər vendor
  signing purpose-u ilə bijective şəkildə bağlıdır:

| Signing purpose | `purpose-short` | P1 canonical selector-lar |
|---|---|---|
| `dobby.plugin-package` | `plugin` | `plugin-prod-2026-01`, `plugin-test-2026-01` |
| `dobby.plugin-license` | `license` | `license-prod-2026-01`, `license-test-2026-01` |
| `dobby.owner-recovery-response` | `recovery` | `recovery-prod-2026-01`, `recovery-test-2026-01` |
| `dobby.branch-setup-authorization` | `branch` | deferred: leaf key və anchor functional BranchSetup işi (Tier B / customer release, `ADR-0056` tier sərhədi) açılana qədər authorize edilmir, format isə artıq bağlıdır |

- `<YYYY-MM>` hissəsi dörd rəqəmli il və `01`–`12` aralığında iki rəqəmli
  aydır. Ay-sız (`plugin-test-2026`), boş və ya purpose-short-u qarışdıran
  selector (`.dobbylicense` üzərində `plugin-prod-2026-01`) canonical **deyil**
  və artifact shape violation sayılır. Bu format **owning schema-lar tərəfindən
  enforce olunur**: hər artifact schema-sı öz purpose-una uyğun `keyId`
  pattern-i daşıyır, çünki yalnız prose-da yazılmış format enforcement deyil.
  Format uyğunluğu key-in trusted olduğunu sübut etmir — verification hər halda
  ring-də exact match tələb edir (§ 4) — lakin yanlış purpose-un selector-unu
  daşıyan artifact signature verification-a **çatmadan** rədd olunur, yəni
  key-purpose confusion üçün əlavə fail-closed bariyer yaranır (`ADR-0059`).
- Device-side purpose-lar (`dobby.owner-recovery-request`,
  `dobby.owner-recovery-request-possession`) vendor selector namespace-inə daxil
  **deyil** və onunla kəsişmir. Host-un non-exportable device recovery key-inin
  selector-u **derived**-dir: `device-` prefiksi plus payload-daki
  `recoveryPublicKeyHash`-ın ilk 16 lowercase-hex simvolu
  (`RecoveryRequestExporter`, `RecoveryRequestVerifier`). Bu selector rotation
  cədvəlinə tabe deyil, çünki key dəyişdikdə hash da dəyişir; ona görə də nə
  `<purpose-short>-<prod|test>-<YYYY-MM>` formatı `device-` slot-unda qəbul
  edilir, nə də `device-` selector-u vendor ring-inə salınır — hər iki hal
  fail-closed rədd olunur. `device-` selector-u müstəqil authority daşımır:
  verifier onu payload-daki hash-dan yenidən hesablayır və bərabər deyilsə
  artifact rədd olunur, yəni proof-un key identity-si imzalanmış payload-a
  bağlıdır (substituted device key qarşısında binding).
- Numeric `keyVersion`
  selector-u **retired**-dir: onu yaradan artifact, schema və kod yoxdur və onu
  oxuyan compatibility reader də yoxdur. Frozen v2 license schema-sı və
  superseded ADR mətnləri onu yalnız historical reference kimi saxlayır. Belə bir
  artifact host-a verilsə signature block shape gate-i onu selector kimi
  interpretasiya etmək imkanı olmadan rədd edir: block **dəqiq** `purpose`,
  `algorithm`, `format`, `keyId`, `value` field-lərini daşımalıdır və əlavə
  `keyVersion` property-si signature verification-dan **əvvəl** shape violation
  kimi fail edir; `schemaVersion` gate-i isə v2 payload vocabulary-sinin heç vaxt
  oxunmamasını təmin edir
  (`docs/schemas/artifact-compatibility-matrix.md`).

### 2. Private key custody (vendor side)

- Hər purpose-un private leaf key-i **ayrı** offline, encrypted media-da
  saxlanılır; iki fərqli fiziki location-da backup tələb olunur.
- Root private key leaf key-lərdən fərqli media-da və fərqli custody prosesində
  qalır.
- Private key heç bir müştəri cihazına, host binary-yə, installer-ə, process
  argument-inə, log-a, report-a və repository-yə daxil edilmir.
- İmza yaratmaq yalnız Admin Tool signing flow-ları vasitəsilə baş verir
  (`docs/specs/admin-tool.md`); private material ilə birbaşa əl işi qadağandır.
- Dev/QA artifact-lar production leaf key-lərlə imzalanmır və `vendorId =
  "dobby.dev-qa"` daşıyır; production host build bu vendor id-ni tanımır.
- İnstaller code-signing certificate-i bu key ring-in hissəsi deyil və onunla
  qarışdırılmır (`ADR-0047`).

### 3. Trust anchor provisioning (host build side)

- Canonical public anchor yolları:

| Purpose | Production anchor | Dev/QA anchor |
|---|---|---|
| `dobby.plugin-package` | `src/Dobby.Host/Trust/vendor-plugin-package-public-key.pem` | `src/Dobby.Host/Trust/dev-qa-plugin-package-public-key.pem` |
| `dobby.plugin-license` | `src/Dobby.Host/Trust/vendor-plugin-license-public-key.pem` | `src/Dobby.Host/Trust/dev-qa-plugin-license-public-key.pem` |
| `dobby.owner-recovery-response` | `src/Dobby.Host/Trust/vendor-recovery-public-key.pem` | `src/Dobby.Host/Trust/dev-qa-recovery-public-key.pem` |

`dobby.branch-setup-authorization` anchor-ı hazırda provision edilmir: Branch
Shared runtime phase-i explicit açılana qədər həmin purpose üçün nə leaf key, nə
anchor faylı yaradılır və host o purpose-u tanımır (`ADR-0056` deferred-by-design).
Purpose açılanda anchor bu cədvələ eyni adlandırma qaydası ilə əlavə olunur.

- Retired `vendor-master-public-key.pem` və `dev-qa-public-key.pem` adları
  dəstəklənmir; həmin adlı fayl mövcud olsa da trust ring-ə daxil edilmir.
- Public anchor-lar **approved build input**-dur və version control altında
  saxlanılır: `.gitignore` `*.pem` qaydası bu altı faylı ignore etməməlidir.
  Private və TEST-ONLY material heç vaxt track edilmir. Public anchor-ın
  version-controlled olmaması release blocker-dır, çünki fresh clone/CI build
  reproducible trust anchor-a malik olmur.
- Anchor-lar compile-time embedded resource kimi daxil edilir (`LogicalName`
  `Dobby.Security.Trust.<file>`); runtime-da filesystem və ya configuration-dan
  trust material yüklənmir.
- Seçilmiş profile-ın anchor-ı yoxdursa build fail-closed olur; həmin yoxlama
  `Dobby.Security.csproj` səviyyəsindədir və silinə bilməz.
- Profile exclusivity: production build-də dev/QA anchor, dev/QA build-də
  production anchor tapılarsa trust resolution `trust-failed` qaytarır və release
  packaging verification fail edir (`ADR-0033` § 2a-dan gələn qayda saxlanılır).

### 4. Rotation via bounded verification rings

- P1 baseline: hər purpose və hər profile üçün host **dəqiq bir** current key
  daşıyır (`plugin-prod-2026-01`, `license-prod-2026-01`,
  `recovery-prod-2026-01` və uyğun `*-test-2026-01`); ring metadata hələ kodda
  mövcud deyil. Bu ADR ring modelini **ikinci key əlavə edilməsindən əvvəl**
  tələb olunan shape kimi bağlayır: rotation ring metadata implementasiya
  olunmayana qədər başlamır, yəni "yeni key-i sadəcə əlavə et" yolu qapalıdır.
- Ring açılanda hər purpose üçün host bounded **verification ring** daşıyır:
  sıfır və ya bir retiring key plus bir current key, hər biri `keyId`,
  `notBefore` və optional `retiredAfter` metadata-sı ilə.
- Verification `keyId` üzrə exact match ilə key seçir; naməlum, retired və ya
  `notBefore`-dan əvvəl istifadə edilən `keyId` fail-closed rədd olunur. Ring
  boyu "try every key" davranışı qadağandır.
- Rotation yeni leaf key-in ring-ə əlavə edilməsi, yeni host build və köhnə
  key-in `retiredAfter` ilə çıxarılması ardıcıllığı ilə aparılır. Ring ölçüsü
  bounded qalır; iki-dən çox eyni purpose key-i eyni build-də olmur.
- Artifact-ın daşıdığı `keyId` downgrade vasitəsi deyil: retired key ilə
  imzalanmış yeni artifact qəbul edilmir, `.dobbylicense` üçün anti-rollback
  `revisionSeriesId`/`licenseRevision` qaydası (`ADR-0060`) müstəqil işləyir.
- Rotation protocol-u bu ADR-in öhdəliyindədir; `ADR-0036` və `ADR-0042`
  rotation owner-i deyil.

### 5. Compromise response

- Compromise leaf key səviyyəsində lokalizə olunur: yalnız həmin purpose-un
  key-i retire edilir, digər purpose-lar və root custody dəyişmir.
- Cavab ardıcıllığı: leaf key-i retire et → yeni leaf key authorize et → yeni
  host build çıxar → müştərilərə out-of-band xəbərdarlıq et → köhnə key ilə
  imzalanmış artifact-ları reject edən ring-i çatdır.
- Root compromise-i tam re-provisioning tələb edir və ayrıca human decision-dur.
- P1-də online revocation (CRL/OCSP) yoxdur; offline-first prinsip saxlanılır və
  revocation host build/ring update ilə çatdırılır.

## Consequences

- `ADR-0033` superseded-dir. Onun tək master key, ortaq plugin/recovery key,
  `vendor-master-public-key.pem` yolu, numeric `keyVersion` selector-u və
  "rotation protocol yoxdur" mövqeyi artıq authority deyil.
- `ADR-0032` public-side verification-ı sahiblənməyə davam edir, amma key custody
  və anchor provisioning hissələri üçün bu ADR-ə istinad edir.
- `ADR-0039` (artıq `ADR-0058` ilə superseded) daxilindəki "recovery vendor master
  key-i paylaşır" mövqeyi authority deyil.
- `keyVersion` terminologiyası heç bir aktiv oxu səthində qalmır — onu oxuyan
  compatibility reader mövcud deyil — və yalnız frozen schema/superseded ADR
  mətnlərində historical reference kimi görünür;
  `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md` bunu legacy kimi
  göstərir.
- `.gitignore` public trust anchor-ları track etməlidir; bu dəyişiklik olmadan
  fresh clone build-i fail-closed dayanır.
- Verification ring metadata (`notBefore`, `retiredAfter`, per-purpose ring) hazırda
  kodda mövcud deyil; P1 hər purpose üçün tək current key daşıyır. Rotation
  ring implementasiya olunana qədər başlaya bilməz və ikinci key əlavə etmək
  bu ADR-in § 4 tələbini pozar.
- Retired master key adı (`Dobby.Security.Trust.vendor-master-public-key.pem`) və
  onun hash-ı release gate-lərində "approved" dəyər kimi qala bilməz; yalnız
  negative control kimi saxlanılır. Gate-lər machine-local publish output-una
  deyil, current build-ə qarşı işləməlidir (`NFR-027`).

## Verification Obligations

Bu ADR-in tələbləri behavior-first sübutla bağlanır (`NFR-027`, `ADR-0061`).
"Fayl mövcuddur", "source-da purpose adı var" və ya "package reference var"
iddiaları aşağıdakı sətirləri qapatmır.

| Yoxlama | Minimum sübut |
|---|---|
| Anchor version control-dadır | Repository hygiene testi: hər canonical anchor yolu `git ls-files` çıxışında görünür; ignore edilmiş anchor fail verir |
| Fresh-clone build | Təmiz checkout üzərində restore/build; anchor faylı silindikdə build fail-closed olur və host binary yaranmır |
| Key-purpose separation | Bir purpose-un key-i ilə imzalanmış digər purpose artifact-ı (license↔package↔recovery↔setup) runtime verification-da rədd olunur |
| Profile exclusivity | Production profile build-ində dev/QA anchor resource-u tapılanda trust resolution `trust-failed` qaytarır; release packaging verification fail edir |
| Ring seçimi | Naməlum `keyId`, `notBefore`-dan əvvəl və retired `keyId` fixture-ları fail-closed; "try all keys" davranışının olmadığını göstərən test |
| Vendor selector formatı | Hər artifact family üçün committed negative fixture: ay-sız və ya cross-purpose `keyId` daşıyan artifact owning schema tərəfindən **həmin `keyId` instance location-ında** rədd olunur; positive fixture canonical selector daşıyır |
| Device selector binding | `possessionProof.keyId` `device-<hash[..16]>` derivation-ından fərqləndikdə runtime verification rədd edir; `device-` slot-unda vendor formatlı selector schema tərəfindən rədd olunur |
| Non-disclosure | Trust failure mesajlarında raw key bytes, tam fayl yolu və exception detail-in olmaması |

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 38 `Plugin Trust Verification`, Row 86
  `Owner Recovery Artefakt Generation`, Cədvəl 3 `Signing Key Custody and Trust
  Anchor Provisioning`,
  § `Key Purpose Separation`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `Vendor plugin/license signing` sətri
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-004`, `NFR-027`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-018`, `AC-038`, `AC-042`
- `docs/09_BUILD_RUN_AND_RELEASE.md` — release packaging və trust profile
  verification
- `docs/adr/ADR-0033-signing-key-ownership.md` — superseded predecessor
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — public-side verification
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — recovery signing
  purpose
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — signature,
  fingerprint və key profile
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — license `keyId` və
  key-purpose separation
- `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md` — installer
  code-signing boundary
- `docs/schemas/dobby-plugin-manifest-v2.schema.json`,
  `docs/schemas/dobby-plugin-license-v3.schema.json`,
  `docs/schemas/dobby-recovery-response-v2.schema.json`,
  `docs/schemas/dobby-recovery-request-v2.schema.json`,
  `docs/schemas/dobby-branch-setup-authorization-v1.schema.json` — per-purpose
  `keyId` selector pattern-lərini enforce edən owning schema-lar
- `src/Dobby.Host/Trust/README.md` — anchor folder qaydaları

## Revision History

- **2026-08-27 (2):** Gap fill, qərar dəyişikliyi deyil, ona görə successor ADR
  yaradılmır. § 1 `dobby.branch-setup-authorization`-u vendor-signed purpose kimi
  sadalayırdı və onun v1 schema-sı ilə fixture-ları mövcud idi, lakin
  `purpose-short` enumerasiyası yalnız `plugin`, `license`, `recovery` idi — yəni
  canonical `keyId` formatı repository-də real olan bir purpose üçün ifadə
  edilə bilmirdi. `branch` purpose-short-u bijective cədvəldə bağlandı. Eyni
  şəkildə device-side selector namespace-i (`device-<recoveryPublicKeyHash
  ilk 16 hex>`) HEAD-də `RecoveryRequestExporter`/`RecoveryRequestVerifier`
  tərəfindən enforce olunmasına baxmayaraq bu ADR-də heç yerdə sənədləşdirilmirdi,
  yəni "canonical `keyId` formatı" iddiası artifact-larda mövcud ikinci
  namespace-i inkar edirdi. Üçüncü boşluq: format yalnız prose-da idi, heç bir
  owning schema onu enforce etmirdi və committed contract fixture-ları ay-sız
  (`plugin-test-2026`, `license-test-2026`, `recovery-test-2026`,
  `branch-test-2026`) selector-larla, `possessionProof.keyId` isə derivation-a
  uyğun olmayan `device-recovery-1` ilə "valid artifact" kimi təqdim olunurdu —
  həmin artifact-ları heç bir host qəbul edə bilməzdi. Format artıq owning
  schema-larda per-purpose pattern kimi enforce olunur, fixture-lar canonical
  dəyərlərə düzəldildi və hər family üçün cross-purpose/ay-sız selector negative
  fixture-u əlavə edildi. Custody, purpose-separation və rotation qərarları
  dəyişməyib.
- **2026-08-27:** Factual reconciliation, qərar dəyişikliyi deyil. İki yerdə
  numeric `keyVersion` selector-unun "v2 compatibility reader-in oxuduğu köhnə
  artifact-larda görünür" kimi təsviri belə bir reader-in mövcudluğunu
  presupposition edirdi; HEAD-də license `schemaVersion != 3`, manifest
  `schemaVersion != 2` və recovery `schemaVersion != 2` artifact-ları hər hansı
  selector interpretasiyasından **əvvəl** fail-closed rədd olunur və heç bir
  legacy reader yoxdur. Yanlış ifadə retired selector üçün mövcud olmayan bir oxu
  səthinə authority verirdi. Mətn həqiqi vəziyyətə uyğunlaşdırıldı və eyni
  over-claim `ADR-0046` terminology cədvəlində də düzəldildi. `ADR-0062`-nin heç
  bir custody, purpose-separation və ya rotation qərarı dəyişmir, ona görə
  successor ADR yaradılmır.
- **2026-08-29:** Verification obligation icrası, qərar dəyişikliyi deyil.
  `Device selector binding` sətri iki istiqamətli iddia daşıyır — `device-`
  slot-unda vendor formatlı selector, **və** vendor ring-inə salınmış `device-`
  selector-u fail-closed rədd olunmalıdır (§ 3) — lakin committed fixture yalnız
  birinci istiqaməti örtürdü
  (`recovery-request-v2.vendor-selector-in-device-slot.invalid.json`). Əks
  istiqamət yalnız prose-da qalırdı, halbuki eyni ADR "yalnız prose-da yazılmış
  format enforcement deyil" deyir. Boşluq təhlükəsizlik baxımından
  asimmetrikdir: `device-` selector-u müstəqil authority daşımayan derived
  dəyərdir, ona görə onun vendor slot-unda qəbulu rotation ring-inə tabe olmayan
  bir selector-a vendor authority-si verərdi.
  `recovery-request-v2.device-selector-in-vendor-slot.invalid.json` əlavə edildi
  və `/payload/vendorKeyId` instance location-ında rədd olunduğu assert olunur.
  Schema artıq düzgün davranırdı (`vendorKeyId` → `#/$defs/keyId`), yəni bu
  davranış dəyişikliyi deyil, örtülməmiş obligation-ın bağlanmasıdır.
