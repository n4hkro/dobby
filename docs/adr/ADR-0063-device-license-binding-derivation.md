# ADR-0063: Device License Binding Derivation

**Status:** Accepted

**Date:** 2026-08-26

**Clarifies:** `ADR-0060` § `Artifact v3` (device binding field), `ADR-0058`
§ `Support identity`, `ADR-0040` § 2 (Row 88 hardware identity responsibility: this ADR
owns the canonical derivation and the normalization that responsibility description
previously implied lived in the adapter)

## Context

`ADR-0060` v3 payload-unda device binding field-i `deviceKeyHash` adı ilə tələb
edir, lakin **dəyərin necə hesablandığını** heç bir Accepted ADR bağlamırdı.
`ADR-0051` (superseded) iki field daşıyırdı — `deviceFingerprintAlgorithm` və
`deviceFingerprint` — v3 isə yalnız bir `sha256` field saxladı, yəni artifact
daxilində derivation version selector-u qalmadı.

Bu boşluq üç ziddiyyət yaratdı:

- `docs/specs/license-import.md`, `docs/specs/device-lifecycle.md` və
  `docs/specs/storage-and-data-protection.md` dəyəri "device identity **public
  key** üzərində license-binding purpose label-ı ilə domain-separated SHA-256
  digest" kimi təsvir edirdi.
- Repository-də license binding üçün **heç bir device identity key pair yoxdur**:
  `ADR-0058` yalnız device **recovery** key pair-ini bağlayır və o key
  `dobby.owner-recovery-request*` purpose-larına aiddir.
- İmplementasiya (`src/Dobby.Licensing/LicenseDeviceFingerprintV1.cs`,
  `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`) dəyəri hardware `MachineGuid`
  və **DPAPI-protected stable device id** üzərindən hesablayır; `ADR-0058`
  § `Support identity` özü də "full **license fingerprint**" terminini istifadə
  edir.

Verification modeli baxımından public-key digest variantı daha zəifdir: import
heç bir device signature yoxlamır, yalnız dəyəri müqayisə edir. Public key
non-secret olduğu üçün DB/state kopyalanmış cihazda eyni digest yenidən istehsal
oluna bilər. Protected stable device id isə machine-bound DPAPI ilə qorunur, yəni
başqa cihazda unprotect uğursuz olur və binding fail-closed olur. Ona görə bu ADR
implementasiya edilmiş derivation-ı **normativ** seçir və spec prose-unu ona
uyğunlaşdırır, tərsinə deyil.

## Decision

### 1. Canonical derivation

- License device binding dəyəri versioned digest-dir:

  `deviceKeyHash = lowercase-hex( SHA-256( canonical-json{ algorithmVersion, machineGuid, stableDeviceId } ) )`

- Sahələr: `algorithmVersion` = `dobby-license-device-fingerprint-v1`;
  `machineGuid` = `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` dəyərinin
  trim + `ToLowerInvariant` + Unicode `FormC` normalizasiyası; `stableDeviceId` =
  protected material-dəki 32-byte random stable device id-nin lowercase hex
  representasiyası.
- Canonical JSON serialization owner-i `LicenseCanonicalJsonWriter`-dir (ordinal
  key sırası, `ADR-0059` canonicalization qaydası ilə eyni yazıçı).
- `machineGuid` boşdursa və ya stable device id mövcud deyilsə derivation
  **fail-closed** olur (`LicenseBindingMismatch`); boş və ya default dəyər ilə
  digest hesablanmır və binding "uğurlu" sayılmır.
- Müqayisə exact və fixed-time-dır (`CryptographicOperations.FixedTimeEquals`);
  fuzzy hardware quorum, partial match və "N-dən M uyğunluq" davranışı qadağandır
  (`ADR-0058` hardware-quorum qadağası).
- Bu dəyər **device identity public key hash-ı deyil**. Recovery device public-key
  hash-ı (`ADR-0058` § `Owner recovery v2`) ayrı purpose-dur, eyni cihazda da
  license binding üçün əvəzlənə bilməz və tərsinə.
- Device private key material license verification-da imza, decrypt və ya
  possession proof üçün istifadə olunmur; license binding sırf value comparison-dur.

### 2. Derivation versioning

- v3 artifact-ı derivation selector daşımır: `schemaVersion: 3` **implicitly**
  `dobby-license-device-fingerprint-v1` deməkdir.
- Derivation dəyişməsi (`-v2`) yeni `schemaVersion` və yeni ADR tələb edir; eyni
  `schemaVersion: 3` altında iki derivation paralel dəstəklənmir və host "hər iki
  variantı sına" davranışı istifadə etmir.
- Köhnə v2 artifact-lardakı `deviceFingerprintAlgorithm`/`deviceFingerprint`
  field-ləri legacy adlardır və host tərəfindən **oxunmur**: v2 compatibility
  reader mövcud deyil, `schemaVersion != 3` artifact payload field-i consume
  edilməmişdən əvvəl fail-closed rədd olunur (`ADR-0060`). Həmin adlar yalnız
  frozen v2 schema-da və negative fixture-larda qalır; yeni artifact onları
  yaratmır və `deviceKeyHash` v1 derivation-ı yeganə canonical binding field-idir.

### 3. Raw material never leaves the device

- Host yalnız **digest**-i ixrac edir. Raw `MachineGuid`, raw stable device id və
  onların hex representasiyası UI-da göstərilmir, audit/log/diagnostics export-a
  düşmür və vendor-a göndərilən heç bir input faylına yazılmır.
- Vendor tərəf (`create-plugin-license`) device binding input-u kimi **yalnız**
  hazır `deviceFingerprint` digest-ini qəbul edir. Raw `machineGuid` və
  `stableDeviceIdHex` field-ləri olan input fail-closed rədd olunur
  (`invalid-device-binding`), çünki belə input raw protected material-in cihazdan
  çıxmasını normalizə edərdi.
- Support surface-lərində göstərilən dəyər license binding digest-i deyil, versioned
  keyed support code-dur (`SupportDeviceCodeV2`, `ADR-0058` § `Support identity`).
- `DeviceIdentity.RedactedSupportPrefix` sütunu **internal uniqueness
  discriminator**-dur: protected stable device id üzərindən domain-separated
  (`dobby-device-identity-discriminator-v1`) truncated digest. O nə support code-dur,
  nə license binding dəyəridir; göstərilmir, export edilmir və authentication/
  authorization qərarında istifadə olunmur.

### 4. Mismatch semantics

- Import zamanı binding uyğunsuzluğu typed reject-dir və mövcud last-known-good
  license state-ini, runtime mode-u və plugin activation state-ini dəyişmir
  (`ADR-0060` LKG qaydası).
- Use time-da persisted state-in binding-i cari cihaz digest-i ilə uyğun gəlmirsə
  runtime mode fail-closed şəkildə denied olur; bu, cross-device state copy
  ssenarisinin gözlənilən nəticəsidir.
- Uyğunsuzluq mesajı non-disclosing-dir: gözlənilən/faktiki digest, raw material,
  registry yolu və exception detail user-visible mesajda, audit-də və diagnostics
  export-unda görünmür.
- Cihaz dəyişdirilməsi (device replacement) yeni digest tələb edir və yeni license
  revision ilə həll olunur; köhnə artifact-ın yeni cihazda qəbulu üçün heç bir
  override, "force bind" və ya offline bypass yoxdur.

### 5. Negative paths: klon, VM və material itkisi

Bu bölmə `ADR-0039` § `Alternatives`-də "tək mənbə cihaz ID-si (yalnız MachineGuid)"
variantının rədd səbəbinə cavab verir. `ADR-0058` protected material modeli o rəddi
köhnəldir, lakin təhdid real qalır və burada açıq yazılmalıdır — əks halda derivation-a
baxan oxucu `MachineGuid`-in klon müqaviməti verdiyini güman edə bilər.

- **Klon müqaviməti `MachineGuid`-də deyil.** Golden master image-dən bərpa edilmiş və ya
  sysprep edilməmiş klonlanmış VM-lər eyni `MachineGuid`-i paylaşa bilər. İki maşını
  fərqləndirən **yalnız** DPAPI ilə qorunan, device-generated, non-exportable stable device
  id-dir (`ADR-0058`). Ona görə stable device id derivation input-u opsional deyil: onsuz
  eyni image-dən çıxan hər klon eyni digest verər və bir license artifact-ı hamısında
  validate olardı.
- **Klonlanmış maşında protected material da klonlanırsa** DPAPI user/machine scope-u
  bunu bloklayır: köçürülmüş blob yeni maşında unprotect olunmur, stable device id
  bərpa olunmur və nəticə § 4 üzrə fail-closed `LicenseBindingMismatch`-dir — səssiz
  yeni identity generasiyası deyil. Bu davranış `ADR-0055` § `OS reinstall` qaydası ilə
  eynidir.
- **Registry manipulyasiyası** `MachineGuid`-i dəyişə bilər. Nəticə mismatch-dir, yəni
  fail-closed; bu, bir hücum vektoru deyil, çünki `MachineGuid`-i dəyişmək bindingi
  genişləndirmir, yalnız cari cihazın öz license-ini sındırır.
- **OS reinstall / motherboard dəyişikliyi** həm `MachineGuid`-i, həm DPAPI material-ını
  itirir; bu, § 4 device replacement yolu ilə həll olunur (yeni revision), recovery ilə
  yox.
- **`MachineGuid` recovery ortasında dəyişirsə** (request export edilib, response import
  edilməmiş) import fail-closed olur; müşahidə olunan davranış
  `docs/specs/session-lifecycle.md` § `Response artifact və import ardıcıllığı`-da
  yazılıb.
- **Gələcəkdə əlavə edilən hardware property** üçün eyni qayda: mövcud olmayan signal
  fail-closed olmalıdır, boş string kimi normalize edilib digest-ə daxil edilməməlidir,
  və `algorithmVersion` artırılmalıdır (§ 2), çünki əks halda mövcud binding-lər səssiz
  şəkildə etibarsız olar.

## Consequences

- Spec prose-u ("device identity public key üzərində digest") artıq authority
  deyil; `docs/specs/license-import.md`, `docs/specs/device-lifecycle.md` və
  `docs/specs/storage-and-data-protection.md` bu ADR-ə uyğunlaşdırılır.
- License binding üçün ayrıca device identity key pair **tələb olunmur**; belə
  key pair-in əlavə edilməsi yeni ADR tələb edir, çünki possession proof olmadan
  binding-i zəiflədir.
- Derivation dəyişikliyi artifact schema version bump-ı ilə bağlıdır; bu, gələcək
  fingerprint hardening-ini planlı migration-a məcbur edir.
- Vendor tərəf digest-only input-a keçdiyi üçün köhnə raw-field fixture-ları
  (`machineGuid` + `stableDeviceIdHex`) artıq qəbul edilmir və yenidən yazılmalıdır.
- `RedactedSupportPrefix` dəyəri domain-separated olduğu üçün köhnə install-larda
  saxlanmış dəyərlə yeni install-lardaki dəyər fərqli derivation-a malikdir; sütun
  heç bir davranışı idarə etmədiyi üçün migration tələb olunmur.

## Verification Obligations

Bu ADR behavior-first sübutla bağlanır (`NFR-027`, `ADR-0061`). "Source-da field
adı var" və ya "fayl mövcuddur" iddiası aşağıdakı sətirləri qapatmır.

| Yoxlama | Minimum sübut |
|---|---|
| Canonical derivation | Fixed `machineGuid`/`stableDeviceId` input-u üçün digest-in dəyişməz olduğunu göstərən test; normalizasiya (whitespace/case/Unicode form) fərqli input-ların eyni digest verdiyini göstərir |
| Fail-closed derivation | Boş `machineGuid` və boş stable device id fixture-ları `LicenseBindingMismatch` qaytarır; digest hesablanmır |
| Exact comparison | Bir simvol fərqli digest rədd olunur; fuzzy/partial match davranışının olmadığını göstərən test |
| Purpose separation | Recovery device public-key hash-ı license `deviceKeyHash` kimi qəbul edilmir və tərsinə |
| LKG qorunması | Binding uyğunsuz candidate import-dan sonra persisted license state, runtime mode və activation state dəyişməz qalır |
| Digest-only vendor input | `machineGuid`/`stableDeviceIdHex` daşıyan binding input-u `invalid-device-binding` ilə rədd olunur və artifact yaranmır |
| Non-disclosure | Binding failure mesajı, audit payload-u və diagnostics export-u raw `MachineGuid`, raw stable device id və tam digest daşımır |
| Discriminator purpose separation | Real migration-dan sonra `DeviceIdentity.RedactedSupportPrefix` dəyərinin eyni stable device id üzərindən hesablanmış license binding digest-inin nə bərabəri, nə prefiksi, nə də substring-i olduğunu göstərən test; binding digest-inin heç bir truncated projection-ı public surface-ə çıxmır |

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Licensing və Device Identity sətirləri
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-009`, `FR-016`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-004`, `NFR-027`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-006`, `AC-020`
- `docs/schemas/dobby-plugin-license-v3.schema.json` — `deviceKeyHash` field
- `docs/specs/license-import.md` — import binding addımları
- `docs/specs/device-lifecycle.md` — device identifier disambiguation
- `docs/specs/storage-and-data-protection.md` — `DeviceIdentity` storage sətri
- `docs/specs/admin-tool.md` — `create-plugin-license` device binding input
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — protected material,
  recovery key purpose və support identity
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — canonicalization
  və key purpose separation
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — v3 artifact və LKG qaydası
- `docs/adr/ADR-0051-license-lifecycle-v2.md` — superseded predecessor (`deviceFingerprint*`
  field adları)

## Revision History

- **2026-08-27:** Legacy field bullet-i düzəldildi; qərar dəyişikliyi deyil. Əvvəlki
  mətn v2 `deviceFingerprintAlgorithm`/`deviceFingerprint` field-lərini "yalnız
  compatibility reader-in oxuduğu" adlar kimi təsvir edir və onların "v1 derivation
  ilə eyni dəyər sayıldığını" deyirdi. İkisi birlikdə iki yanlış nəticə verirdi:
  mövcud olmayan bir reader-ə authority verirdi (HEAD-də `schemaVersion != 3`
  payload field-i consume edilməmişdən əvvəl fail-closed rədd olunur) və v2
  binding field-inin v3 `deviceKeyHash` ilə **əvəzlənə bilən** olduğunu ima edirdi
  — bu, binding downgrade səthidir. Mətn indi field-lərin oxunmadığını və
  `deviceKeyHash` v1 derivation-ının yeganə canonical binding olduğunu deyir.
  Derivation, versioning və digest-only qaydaları dəyişmir.

- **2026-08-29:** § 3 `RedactedSupportPrefix` qaydası koda enforce edildi və
  Verification Obligations-a "Discriminator purpose separation" sətri əlavə edildi;
  qərar dəyişikliyi deyil, mövcud normativ mətnin icrası. Kod tərəfində iki fərqli
  drift vardı: (a) `HostPersistenceMigrator` sütunu domain separation olmadan
  (`SHA256(stableDeviceId)[..12]`, uppercase hex) hesablayırdı, yəni bu ADR-in
  tələb etdiyi `dobby-device-identity-discriminator-v1` domain-i yox idi;
  (b) `LicenseDeviceFingerprintV1` record-u `RedactedPrefix` adlı ikinci member
  daşıyırdı və dəyəri **license binding digest-inin ilk 12 hex simvolu** idi —
  domain separation-sız, sənədləşdirilməmiş və heç bir production consumer-i
  olmayan. (b) daha təhlükəli idi: adı ("redacted") onu display-safe support
  identifier kimi təqdim edirdi, halbuki § 3 yeganə human-facing identifier kimi
  `SupportDeviceCodeV2`-ni təyin edir. Binding possession proof olmadan sırf value
  comparison olduğu üçün digest-in hər hansı prefiksinin publish olunması offline
  guessing qısayoludur. Ona görə `RedactedPrefix` member-i silindi (breaking
  consumer yox idi — full solution build 0 warning), tipə niyə heç bir truncated
  projection saxlanılmadığını izah edən remark əlavə edildi, migrator domain-separated
  lowercase derivation-a keçirildi və separation real DB üzərində
  `Persisted_device_discriminator_is_domain_separated_from_the_license_binding_digest`
  testi ilə bağlandı. Derivation, versioning, digest-only və non-disclosure
  qaydaları dəyişmir.
