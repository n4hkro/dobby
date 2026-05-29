# ADR-0039: Owner Password Recovery via Vendor-Signed Device-Bound Artefakt

**Status:** Accepted
**Date:** 2026-04-23

## Context

`docs/01_PRODUCT_SCOPE.md` Owner Recovery bölməsi (L56-68) məhsul səviyyəsində
bir axın təsvir edir: istifadəçi cihazın unikal kimliyini vendor-a göndərir,
vendor admin tool vasitəsilə artefakt hazırlayır, müştəri həmin artefaktı app-a
təqdim edir və Owner şifrəsi sıfırlanır. ADR-0038 Local-First Authentication
isə auth axınının tam offline işləməli olduğunu təsbit edir — recovery də
bu prinsipi pozmamalıdır.

Scope məhsul davranışını bağlayır, lakin texniki suallar açıqdır:

- cihaz kimliyi hansı mənbədən çıxarılır və hansı namespace ona sahib olur
- vendor artefaktı hansı formatda, hansı açarla imzalanır və host onu necə
  doğrulayır
- replay, theft, expired və cross-device istifadə hallarına qarşı hansı
  qorunma mövcuddur
- artefakt generation (vendor side) və verification (host side)
  mülkiyyətləri hansı namespace-lərə düşür

Bu ADR həmin suallara konkret cavab verir və recovery axınının protokolunu
birmənalı bağlayır.

## Decision

Owner password recovery axını yalnız **Owner hesabı** üçündür və **fayl əsaslı
export/import** modeli üzərində, **vendor-signed device-bound artefakt** ilə
işləyir. Artefakt **24 saatlıq müddətə malikdir**, **yalnız bir dəfə istifadə
oluna bilir** və **yalnız həmin cihazda** qüvvədədir.

Recovery request/response formatı generic target-user modeli daşımır.
`targetUserId`, `targetUsername` və oxşar managed-user target field-ləri bu
axında yoxdur. Host recovery import zamanı lokal Owner hesabını implicit target
kimi qəbul edir. Managed user password reset Owner və ya uyğun icazəsi olan
user tərəfindən User Management içində aparılır və bu ADR-in recovery
artefakt kanalına daxil deyil.

### 1. Mübadilə axını

Client və vendor admin tool arasında fayl əsaslı mübadilə. Şəbəkə tələb
olunmur:

1. Host `Export Recovery Request` → `.dobbyrecoveryrequest` request faylı yaradır
2. İstifadəçi faylı out-of-band kanalla vendor-a ötürür (email, USB, file share)
3. Vendor admin tool → input request əsasında, operatora user seçimi vermədən,
   signed `.dobbyrecovery` response artefaktı yaradır
4. İstifadəçi cavab faylını geri alır
5. Host `Import Recovery Response` → faylı yoxlayır və qəbul edirsə parol
   reset dialog-unu açır

### 2. Cihaz fingerprint-i

5 mənbədən kompozit hash:

- Windows MachineGuid (`HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`)
- CPU ID (WMI `Win32_Processor.ProcessorId`)
- Motherboard serial (WMI `Win32_BaseBoard.SerialNumber`)
- Primary disk serial (WMI `Win32_DiskDrive.SerialNumber`)
- Primary MAC address (`NetworkInterface.GetPhysicalAddress`)

Hər mənbə ayrıca SHA-256 ilə hash-lanır. Plain text ID göndərilmir; yalnız
hash-lar artefakt daxilində yer tutur.

**Fuzzy matching:** verifikasiya zamanı cari cihazın 5 hash-ından **ən az 3-ü**
artefakt-dakı hash-larla uyğun gəlməlidir. Bu model disk dəyişdirmə, OS
yenidən quraşdırılması və ya MAC dəyişikliyi kimi normal dəyişikliklərə qarşı
davamlılıq verir, amma fiziki cihaz klonlanmasını çətin saxlayır.

### 3. Request faylı (müştəri → vendor, imzasız)

Request fayl extension-ı `.dobbyrecoveryrequest` olur. Format Owner-only
modeldir; target user field-i daşımır.

```json
{
  "type": "owner-recovery-request",
  "dobbyVersion": "1.0.0",
  "requestedAt": "<ISO 8601 UTC timestamp>",
  "deviceIdentity": {
    "machineGuid":       "<sha256 base64>",
    "cpuId":             "<sha256 base64>",
    "motherboardSerial": "<sha256 base64>",
    "diskSerial":        "<sha256 base64>",
    "macAddress":        "<sha256 base64>"
  },
  "deviceDisplayName": "<optional, e.g. DESKTOP-ABC123>",
  "requestId": "<UUID v4>"
}
```

Request faylı imzalanmır — trust tələb etmir. Vendor müştərini başqa
kanallarla tanıyır (müqavilə, email). Request yalnız device/request identity
daşıyır; hansı user-in reset olunacağını seçdirmir, çünki target həmişə lokal
Owner hesabıdır.

Recovery request faylları **identity request**-ləridir, sirr deyil. Onlar inspect edilə bilən support artefaktları kimi nəzərdə tutula bilər; trust yalnız vendor-signed response host tərəfindən yoxlanılanda qurulur.

### 4. Response artefaktı (vendor → müştəri, imzalı)

Response fayl extension-ı `.dobbyrecovery` olur. Admin Tool response yaradarkən
operator ayrıca user seçmir; response yalnız input request-dəki requestId və
deviceIdentity əsasında yaranır.

```json
{
  "payload": {
    "type": "owner-recovery-response",
    "requestId": "<echoes request>",
    "deviceIdentity": { 5 hashes — request-dən kopyalanır },
    "issuedAt":  "<ISO 8601 UTC>",
    "expiresAt": "<issuedAt + 24h>",
    "nonce":      "<16 random bytes, base64>",
    "keyVersion": 1
  },
  "signature": "<base64 of IEEE P1363 r||s, ECDSA P-256 over SHA-256 hash of JCS canonical payload bytes>"
}
```

İmza payload object-in **RFC 8785 (JCS) canonical JSON** byte-larının
(UTF-8, no BOM) SHA-256 hash-i üzərində **ECDSA P-256** ilə yaradılır.
Signature output IEEE P1363 64-byte `r||s` formasında olur və RFC 4648 §4
standard base64 (padding ilə, `+/=` alphabet) ilə kodlanır. ASN.1 DER
signature qəbul edilmir. Vendor master public key Dobby host binary-yə
compile-time resource kimi embed olunur.

`payload.keyVersion` numeric JSON dəyəridir və `ADR-0033` canonical key-version
qaydasına tabedir; Phase 1 dəyəri `1`-dir və string `"v1"` forması recovery
response artifact-ında qəbul edilmir.

Recovery imzalamaq üçün istifadə olunan vendor açarı **plugin signature
trust model** ilə ortaq master key-dir (ADR-0033 scope-u ilə paylaşılır).
Phase 1-də ayrı recovery açarı saxlanmır.

### 5. Host verifikasiya ardıcıllığı

Sıra məcburidir — imza ilk olaraq yoxlanır, çünki imza keçməyən faylın
payload məzmununa etibar etmək olmaz:

1. Faylı oxu, JSON parse et
2. Vendor public key ilə imzanı yoxla
3. `expiresAt > now` yoxla
4. Nonce-un local consumed DB-də olub-olmadığını yoxla
5. Cari cihazın 5 fingerprint hash-ını hesabla
6. Artefakt hash-ları ilə müqayisə et; **≥3 uyğunluq** tələb olunur
7. Hamısı keçdisə parol reset dialog-unu aç
8. Host lokal Owner hesabını tapır; Owner yoxdursa və ya birdən çox Owner
   invariant pozulubsa import fail-closed rədd edilir
9. Parol uğurla yenilənəndən **sonra** nonce-u consumed qeyd et (atomik
   transaction: Owner password update + nonce burn)

İstifadəçi parol reset dialog-unu ləğv etsə, nonce hələ də consumed deyil
və artefakt müddət bitənə qədər istifadə oluna bilər.

### 6. Replay qorunması

Local SQLite cədvəl (`Dobby.Infrastructure` persistence boundary-si daxilində,
ADR-0030 uyğun):

```
ConsumedRecoveryNonces:
  Nonce       TEXT PRIMARY KEY
  ConsumedAt  DATETIME
  RequestId   TEXT
```

Cədvəl **heç vaxt silinmir**. Artefakt-ın müddəti bitsə də, nonce consumed
statusu saxlanır. Bu defense-in-depth sistem saatının manipulyasiyasına qarşı
qorunma verir.

### 7. Audit qeydi

Hər recovery cəhdi (uğurlu və uğursuz) `Dobby.Audit / Audit Trail` (Row 70)
vasitəsilə qeyd olunur:

- timestamp
- requestId (artefakt-dan)
- nəticə: `success` / `signature-fail` / `expired` / `replay` / `device-mismatch`
- `nonceHash` (SHA-256 of nonce, lowercase/base64 or equivalent non-reversible
  representation; raw nonce audit və diagnostics payload-a yazılmır)

### 8. Namespace mülkiyyət

- Artefakt imza verifikasiyası, device fingerprint müqayisəsi, nonce consume
  məntiqi: `Dobby.Security / Authentication / Session` (Row 35)
- Win32 API inteqrasiyası (Registry, WMI, NetworkInterface) via hardware
  identity query səthi: `Dobby.Infrastructure`
- Consumed nonce persistence: `Dobby.Infrastructure` (ADR-0030 boundary ilə)
- Audit logging: `Dobby.Audit / Audit Trail` (Row 70)
- Artefakt generation (vendor side): `Dobby.Admin / Owner Recovery Artefakt
  Generation` (Row 86) — host binary-dan kənar. Admin Tool input request-dən
  başqa user seçimi qəbul etmir.

### 9. Out of scope

- Vendor açar rotasiyası protokolu — `keyVersion` field forward-compatibility
  saxlayır, amma rotation protokolu ayrıca ADR-də bağlanır
- Non-Owner istifadəçilər üçün recovery axını — normal istifadəçilər Owner və
  ya uyğun icazəsi olan user tərəfindən User Management içində reset olunur;
  artefakt kanalı yalnız Owner lockout-u üçündür
- Cloud/online recovery alternativi — offline-first prinsipi ilə uyğunsuzdur
- Vendor admin tool-un konkret UI dizaynı — `Dobby.Admin` layer-də bağlanır
- Artefakt format versiya migrasiyası — Phase 1-də yalnız version 1

### 10. Rədd edilən alternativlər

- **TPM-bound identifier:** rədd edildi — `03_TECH_STACK_AND_VERSIONS.md`
  Windows 10 LTSC/ESU baseline-ını da dəstəkləyir, TPM 2.0 universal deyil
- **Ayrı recovery signing key:** P1-də deferred — solo dev operasional
  sadəliyi; `keyVersion` field gələcəkdə yüksəlişə imkan verir
- **Müddətsiz artefakt:** rədd edildi — replay pəncərəsi geniş olur; 24 saat
  enterprise recovery turnaround-u üçün kifayətdir
- **Tək mənbə cihaz ID-si (yalnız MachineGuid):** rədd edildi — OS reinstall
  və ya registry manipulyasiyası ilə çox asan pozulur
- **Müştərinin özü artefakt yaratması (self-service):** rədd edildi — vendor
  trust anchor-u aradan çıxar və Owner lockout-u brute-force-a açar

## Consequences

- `02_ARCHITECTURE_MAP.md` Row 35 `Authentication / Session` deskripsiyasına
  recovery artefakt verification məsuliyyəti əlavə olunur
- `02_ARCHITECTURE_MAP.md` Row 86 `Owner Recovery Artefakt Generation`
  namespace-i bu ADR üçün minimal CLI/surface məsuliyyətini daşıyır;
  Row 85 `Admin Application Composition` tam admin UI (P1-if-time) kimi ayrıldı
- `02_ARCHITECTURE_MAP.md` Cədvəl 3-ə yeni risk sətri əlavə olunur (`Owner
  Password Recovery Flow`)
- `docs/01_PRODUCT_SCOPE.md` Owner Recovery bölməsi konkretləşdirilir: 24
  saatlıq müddət, fayl əsaslı mübadilə və tək istifadə explicit qeyd olunur
- `Dobby.Infrastructure` Win32 API inteqrasiyasını (Registry, WMI,
  NetworkInterface) hardware identity query səthi kimi açır — bu səth
  `Dobby.Security`-dən çağrılır
- Host binary build pipeline-ı vendor master public key-i compile-time
  resource olaraq embed etməlidir (plugin signature verification ilə
  ortaqdır)
- Local SQLite schema-ya `ConsumedRecoveryNonces` cədvəli əlavə olunur
  (ADR-0030 host persistence boundary-si daxilində)
- ADR-0033 `Signing key ownership` vendor master key-in idarəsini bağlayır;
  recovery Phase 1-də həmin açarı paylaşır, `keyVersion` field gələcək
  rotasiya üçün forward-compatibility saxlayır
- Vendor admin tool (`Dobby.Admin`) recovery artefakt generation flow-unu
  daşımalıdır; konkret UI və key handling admin layer-də bağlanır. Recovery
  generator user picker göstərmir və response-u yalnız input request əsasında
  yaradır

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — `## Owner Recovery / Reset Password`
- `docs/02_ARCHITECTURE_MAP.md` — Row 35 `Authentication / Session`
- `docs/02_ARCHITECTURE_MAP.md` — Row 86 `Owner Recovery Artefakt Generation`
- `docs/02_ARCHITECTURE_MAP.md` — Row 85 `Admin Application Composition` (tam admin UI, P1-if-time)
- `docs/02_ARCHITECTURE_MAP.md` — Row 70 `Audit Trail`
- `docs/adr/ADR-0038-local-first-authentication.md` — offline-first və device-bound auth əsası
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — nonce DB host data store-da
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — ortaq signature verification infrastrukturu (bu ADR recovery ilə paylaşılır)
- `docs/adr/ADR-0033-signing-key-ownership.md` — vendor master key mülkiyyəti və provisioning prosesi (Phase 1-də recovery bu açarı paylaşır; MF-04 vendor identity provisioning burada bağlanır)
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md` — `Dobby.Infrastructure` ownership (hardware identity + consumed nonce persistence)
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` — recovery secret və consumed nonce material üçün protected-storage scope

## Revision History

- **2026-05-16:** Response artifact §4 sample və prose deterministic wire format
  üçün dəqiqləşdirildi: `payload.keyVersion` numeric JSON authority-dir,
  `signature` top-level standard base64 string-dir, signed bytes payload-only
  RFC 8785/JCS canonical JSON (UTF-8 no BOM) SHA-256 hash-idir, signature
  ECDSA P-256 IEEE P1363 64-byte `r||s` formatındadır, DER signature qəbul
  edilmir. JCS implementation Sprint 5-də internal recovery-payload-scoped
  writer kimi qalır; yeni package pin açılmır.

## Branch Shared Recovery Amendment

Owner recovery remains user-action-driven and is not auto-generated by
BranchSetup. Branch Shared setup proof reuses `.dobbylicense` as read-only setup
evidence; it does not create `.dobbyrecoveryrequest`.

Branch Shared pre-auth recovery audit events (`recovery.export` and
`recovery.import`) follow the narrow `ADR-0057` PostgreSQL audit exception when
available. If PostgreSQL is unreachable before that insert succeeds, local
evidence is diagnostics/setup evidence only.
