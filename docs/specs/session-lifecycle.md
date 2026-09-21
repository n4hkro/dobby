# Session Lifecycle Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-001`, `FR-021`, `FR-022`, `FR-023`, `FR-024`, `FR-031`, `AC-001`, `AC-026`, `AC-028`, `AC-029`, `NFR-002`, `NFR-025`, `NFR-027`, `ADR-0058`, `ADR-0061`, `ADR-0031`, `ADR-0038`, `ADR-0050`, `docs/specs/work-sessions.md`
**Controlling decisions:** remembered-login material, durable throttling,
password-change proof və Owner recovery v2 `ADR-0058`-in öhdəliyindədir və
`ADR-0038`/`ADR-0039`-un saved-credential, app-wide throttle və fuzzy
hardware-match mətnini əvəz edir; pre-auth runtime boundary və atomic mutation
contract-ı `ADR-0061`-dədir. Machine-readable authority:
[`docs/schemas/dobby-recovery-request-v2.schema.json`](../schemas/dobby-recovery-request-v2.schema.json)
və [`docs/schemas/dobby-recovery-response-v2.schema.json`](../schemas/dobby-recovery-response-v2.schema.json).

## Məqsəd

Bu spec login sonrası user session davranışını, logout cleanup-ını, lock/auto-lock axınını və password change-in session-a təsirini tək yerdə bağlayır.

## Canonical Qaydalar

- Eyni Dobby prosesi daxilində yalnız bir active user session ola bilər.
- Login ekranı görünürsə, əvvəlki authenticated session artıq bitmiş sayılır.
- Remember-me authenticated session deyil və secret saxlamır; yalnız pre-auth login form-unda username prefill rahatlığıdır.
- Logout current session-u bitirir; lock current session-u saxlayır.
- Work session-lar yalnız post-login process memory-də saxlanır; restart sonrası host restore P2-dir.
- Plugin-in real/draft/domain datası ayrıca plugin storage məsuliyyətidir.
- Single-instance coordinator login UI-dan əvvəl acquire olunur; pre-login consumer-lər direct SQLite/Dapper deyil, repository/application-service contract-ları istifadə edir.
- Pre-auth mərhələdə yalnız `ADR-0061` allow-list-i işləyir: single-instance coordinator, minimum theme/localization, username-only remembered-login metadata, credential/throttle store-unu açmaq üçün lazım olan protected material, credential/throttle sorğuları, Owner recovery verification və closed pre-auth audit set. Forbidden I/O edən service-in konstruksiyası da qadağan olunan iş sayılır.
- Authentication uğuru əvvəlcə session-u commit edir, sonra post-login scope-u yaradır; logout həmin scope-u dispose edir.

## Session Açılışı

Uğurlu login zamanı host:

1. session-u commit edir və current user identity yaradır;
2. session material/protected session state yaradır;
3. permission cache-i həmin user üçün fresh başlayır;
4. session commit-dən **sonra** authenticated scope-u yaradır; plugin runtime və user-scoped plugin service state-i yalnız orada açılır;
5. yalnız həmin account/authenticator-a aid throttle bucket-larını təmizləyir.

Uğurlu authentication device/global pressure bucket-in aktiv cəzasını sıfırlamır
və başqa account-ların bucket-larına toxunmur (`ADR-0058`, `FR-022`).

Session commit, remembered-account slot dəyişikliyi və login audit qeydi bir
atomik commit sərhədində baş verir; commit uğursuz olarsa authenticated scope
yaradılmır və success publish olunmur (`ADR-0061`, `NFR-025`).

First-run Owner bootstrap `FR-002` və `ADR-0041` ilə idarə olunur.

Login credential input validation `FR-032` və `docs/specs/user-management.md` Credential Validation section-ı ilə idarə olunur. Invalid username formatı və boş/uzun password login attempt başlamazdan əvvəl typed validation error verir; authentication failure-ları isə normal failed-login throttle qaydasına düşür.

## Pre-auth interaction details

Login surface açıldıqda username field fokuslana bilər. Password field proses
restart-ından sonra həmişə boş açılır; masked placeholder retained password
yerinə keçmir və stored password üçün reveal action yoxdur (`ADR-0058`). Enter
yalnız form valid və submit enabled olduqda login attempt göndərir. Failed-login
throttle countdown aktivdirsə submit disabled qalır və countdown localized text
ilə göstərilir.

## Failed-Attempt Throttling

Login və unlock throttle davranışı `FR-022`, `AC-026` və `ADR-0058` ilə bağlanır;
bu spec onun session-facing nəticələrini saxlayır:

- throttle state durable-dır, `TimeProvider` üzərindən ölçülür və proses
  restart-ından sonra qalan cəza müddətini bərpa edir;
- ən azı iki müstəqil bucket işləyir: normalize olunmuş account və device/global
  pressure bucket; username dəyişmək device/global cəzanı sıfırlamır və bir
  account-un cəzası başqa account-u bloklamır;
- naməlum və mövcud account eyni user-facing timing və error davranışı göstərir;
  UI mesajı account-ın mövcudluğunu açıqlamır;
- password verification-ın tək bounded sahibi var; UI text dəyişikliyi limitsiz
  KDF işi başlatmır. Credential warm-up saxlanılarsa cancellable,
  concurrency-bounded, one-shot, short-lived olur və eyni throttle ledger-inə
  yazılır;
- bounded verification capacity aşılanda artıq cəhdlər authentication failure
  kimi deyil, `ResourceBusy` kimi görünür;
- lock screen unlock cəhdləri eyni ledger-ə düşür;
- throttle qərarları deterministik clock/scheduler seam-i ilə test olunur; real
  timing sleep-i acceptance evidence deyil.

## Logout

Logout entry point-ləri:

- profile menu;
- lock screen-dəki logout action.

Logout app-i bağlamır; user-i login ekranına qaytarır.

Logout cleanup minimumu:

- current user identity silinir;
- session material/protected session state silinir;
- cached permission decisions təmizlənir;
- in-memory plugin services və user-scoped runtime state dispose/clear edilir;
- active work session varsa user-ə warning göstərilir;
- user təsdiqləyəndən sonra active work session-lar controlled close/cancel edilir.

Logout remembered-account slot-unu silmir: slot yalnız normalize olunmuş
username və non-secret slot identifier saxladığı üçün onun qalması credential
saxlamaq deyil. Slot növbəti uğurlu login zamanı remember-me OFF seçildikdə,
həmçinin account silinəndə/deaktiv ediləndə təmizlənir.

## Remembered Login Qarşılıqlı Təsiri

Remembered login qaydası `ADR-0058`-ə tabedir (`ADR-0038`-in saved-credential
mətnini əvəz edir):

- auto-login yoxdur və session refresh yaradılmır;
- persist olunan yeganə material normalize olunmuş username və non-secret slot
  identifier-dir; password, reversible credential, password-equivalent verifier,
  startup snapshot və ya bunların masked surrogate-i saxlanmır;
- login form-unda yalnız username prefill görünür; password field boş qalır,
  masked placeholder retained password yerinə keçmir və reveal action yoxdur;
- remembered slot authentication factor deyil: onun mövcudluğu heç bir yoxlamanı
  atlamır və hər login-də tam credential verification tələb olunur;
- persistent slot yalnız uğurlu login zamanı remember-me OFF seçildikdə
  boşaldılır; hər uğurlu login remember-me aktiv olduqda slot-u yeni normalize
  olunmuş username ilə replace edir;
- legacy saved-password qeydləri və duplicate startup snapshot idempotent
  migration ilə silinir: legacy slot table-ı password-equivalent protected blob
  sütunu olmadan yenidən qurulur və legacy sətir yalnız real `Users` sətri ilə
  tam reconcile olunduqda carry-forward edilir; reconcile olunmayan slot bərpa
  edilmir, yəni upgrade-dən sonra prefill boş görünə bilər və user username-i bir
  dəfə yenidən yazır (canonical qayda: `docs/specs/storage-and-data-protection.md`
  → "Remembered-login slot legacy migration (owning rule)");
- gələcək one-click device login ayrıca authenticator feature-dir: random,
  revocable, purpose-bound device credential tələb edir və password masking kimi
  təqdim edilmir.

Logout və auto-lock remembered slot-u silmir.

## Lock və Auto-Lock

Manual lock entry point-ləri:

- profile menu;
- status area və ya shell-in host-controlled quick action səthi.

Auto-lock default passivdir. User aktivləşdirəndə timeout dəyərini dəqiqə ilə verir.

Lock screen:

- current session context-i saxlayır;
- cari user üçün password field və unlock action göstərir;
- logout button göstərir;
- başqa user ilə birbaşa login flow-u göstərmir.

Lock screen aktiv olduğu müddətdə authenticated shell content-i **gizlədilir**. Lock
input-u bloklamaqla kifayətlənmək yetərli deyil: minimized work strip label-ları,
plugin page məzmunu, device code və user identity kimi authenticated surface-lər
locked vəziyyətdə oxunaqlı qalmamalıdır, çünki lock-un məqsədi nəzarətsiz qalmış
workstation-da məlumatı qorumaqdır. Concealment window state-ini dağıtmır: unlock
uğurlu olduqda eyni process-lifetime shell surface-i əvvəlki görünüşü ilə bərpa
olunur, work session-lar isə bağlanmır.

Lock screen-də göstərilən identity minimum lazım olan qədərdir (cari user
identifikatoru) və business/plugin data, license detalları və ya audit məzmunu lock
screen-də göstərilmir.

Başqa user login etmək üçün lock screen-də logout seçilir və login ekranına qayıdılır.

Advanced auto-lock preset/timer UI və lock screen visual polish P3 scope-dur; `docs/02_ARCHITECTURE_MAP.md` Future Scope cədvəlinə bax.

## Lock Screen Outcomes

Unlock uğursuz olarsa lock screen current session context-i saxlayır və typed, non-raw error göstərir. Deactivated user unlock edə bilməz; session invalid olur və login ekranına qaytarılır. Failed-attempt countdown varsa lock screen-də göstərilir.

Cancel current lock screen-i bypass etmir; user ya unlock edir, ya logout seçir.

## Lock screen interaction details

Password field ilkin focus ala bilər. Unlock primary action, logout secondary action kimi render olunmalıdır; action priority lock bypass yaratmamalıdır.

## Password Change

Self-service password change profile/account səthindən cari authenticated user üçün aparılır.

Uğurlu self-service password change:

- cari password və eyni new-password confirmation tələb edir;
- cari session-u invalid etmir;
- həmin user-in başqa active session-larını `password-change` səbəbi ilə invalid edir;
- current user-i login ekranına məcburi qaytarmır;
- növbəti login üçün yeni password tələb edir;
- active authenticated session tələb edir; login/pre-auth səthindən self-service
  password change yoxdur;
- remember-me slot yalnız username/account metadata saxlayır və seçimə görə həmin
  account ilə replace edilir və ya clear olunur; password materialı saxlanmır;
- session material yeni credential version metadata ilə rebind olunur;
- failure mesajları non-disclosing qalır və raw password/hash detail göstərmir;
- yanlış current-password proof durable throttle ledger-inə yazılır və
  non-disclosing deny verir;
- credential, current/other session, remembered-account, audit və outbox state-i
  bir atomik commit sərhədində dəyişir; injected commit failure köhnə
  credential/session/audit vəziyyətini olduğu kimi saxlayır və success publish
  olunmur.

Password change proof-u UI-level onay ilə əvəz edilə bilməz: current password
(və ya explicit olaraq təsdiqlənmiş ekvivalent reauthentication ceremony) tələbi
service boundary-də yoxlanılır. Admin reset və Owner recovery ayrı flow-lardır və
köhnə password tələb etmir (`ADR-0058`).

User Management daxilində başqa managed user üçün password reset recovery deyil. Əgər target user-in active session-ı varsa, həmin session invalid edilir.

## Owner Recovery (v2)

Owner password recovery vendor-assisted pre-auth flow-dur və `ADR-0058` ilə idarə
olunur; bu model `ADR-0039`-un fuzzy hardware-quorum binding-ini əvəz edir.
Managed user password reset recovery deyil (`docs/specs/user-management.md`).

### Device recovery key material

Host protected-material bootstrap zamanı random device recovery key pair yaradır.
Private key application boundary-dən export olunmur və restrictive ACL ilə
protected storage-də qalır; public key və versioned key id non-secret-dir
(`ADR-0058`, `ADR-0055`). Bu key pair license-binding device identity key-i,
plugin-package trust material-ı və setup-authorization key-i ilə əvəzlənmir
(`ADR-0059` key-purpose separation).

Hardware property-ləri yalnız support-safe risk signal-ı ola bilər: authentication
factor, quorum hash, encryption key və ya recovery binding authority deyil. Phase
1-də faktiki toplanan yeganə hardware property `MachineGuid`-dir; `ADR-0039`-un
CPU/board/disk/MAC oxunuşları koddan silinib (`ADR-0040`
§ `2026-08-29 Collected-property narrowing`), ona görə yuxarıdakı qadağa hazırda
`MachineGuid`-dən başqa heç bir toplanmış dəyərə tətbiq olunmur.

### Request artifact

`.dobbyrecoveryrequest` canonical shape-i
[`dobby-recovery-request-v2.schema.json`](../schemas/dobby-recovery-request-v2.schema.json)
tərəfindən sahiblənir: `schemaVersion: 2`,
`purpose: dobby.owner-recovery-request`, `requestId`, random `nonce`,
`customerId`, `deploymentId`, `deviceId`, `recoveryPublicKeyHash`, `issuedAt`,
`expiresAt`, `vendorKeyId` və device possession proof
(`purpose: dobby.owner-recovery-request-possession`, `ECDSA-P256-SHA256`,
`IeeeP1363FixedFieldConcatenation`, unpadded base64url). Possession proof
canonical request payload üzərində device recovery private key ilə yaradılır.

`possessionProof.keyId` device-side selector-dur və vendor selector namespace-i
ilə **disjoint**-dir: dəyəri `device-` + signed `payload.recoveryPublicKeyHash`-in
ilk 16 lowercase hex simvoludur (`ADR-0062` § 1). Bu selector rotate olunmur,
çünki o key material-ın özündən derive olunur; verifier onu artifact-dan
oxumaqla kifayətlənmir, signed hash-dan yenidən hesablayır və uyğunsuzluğu
rədd edir — əks halda substituted device key əlaqəsi olmayan identity altında
proof təqdim edə bilərdi. Schema həmin slot-da vendor formatlı selector-u
(`recovery-prod-…`) və vendor `vendorKeyId` slot-unda `device-` selector-u
fail-closed rədd edir; `vendorKeyId` isə cihazın gözlədiyi vendor recovery
key-inin canonical selector-udur.

`nonce` cryptographically secure RNG-dən gəlir və **256-bit** entropiya daşıyır:
schema onu tam 43 simvollu unpadded base64url dəyər kimi bağlayır. Nonce vaxt
dəyərindən, counter-dən, `requestId`-dən, device id-dən, username-dən və ya
hər hansı predictable material-dan derive edilmir; qısa, non-canonical, padded və
ya alphabet-dən kənar dəyər schema validation-da `invalid-recovery-file` typed
reject-i alır və heç bir credential mutation başlatmır. Eyni nonce iki request
üçün yenidən istifadə edilmir.

Artifact-dakı `deviceId` non-secret device reference-dir:

- Branch Shared deployment-də server-issued `deviceId`
  (`docs/specs/device-lifecycle.md`);
- Standalone-da protected stable device id üzərində **recovery-binding purpose
  label-i** ilə hesablanmış versioned, domain-separated keyed digest.

Raw `DobbyStableDeviceId`, raw MachineGuid, raw recovery public key və full
license fingerprint nə artifact-a, nə UI-a, nə audit-ə, nə log-a, nə də export-a
düşür (`ADR-0058`). Bu dəyər license `deviceKeyHash`-ı ilə eyni deyil və onunla
əvəzlənə bilməz (`docs/specs/license-import.md`).

Request yaradılması pre-auth səthdən mümkündür və authenticated session tələb
etmir. Pending request local **request ledger**-ində saxlanılır (pre-login closed
table set-indəki `RecoveryRequests`,
[`storage-and-data-protection.md`](./storage-and-data-protection.md)) və bu
ledger response import-unun **primary** replay/binding gate-idir. Saxlanılan
state:

- `requestId` (UUID primary key) və binding dəyərləri `customerId`,
  `deploymentId`, `deviceId`, `recoveryPublicKeyHash`, gözlənilən `vendorKeyId`;
- nonce **raw saxlanmır**: yalnız onun 64-hex SHA-256 hash-ı (`NonceHash`)
  persist olunur və o, ledger-də globally unique-dir, yəni iki pending request
  eyni nonce ilə mövcud ola bilməz;
- exported payload üzərində hesablanmış `PayloadHash`: import zamanı payload
  yenidən qurulur və hash müqayisə olunur, beləliklə persist olunmuş pending
  record-un sonradan dəyişdirilməsi (malformed/tampered local state) aşkarlanır
  və `binding-mismatch` ilə fail-closed rədd olunur;
- `issuedAt`/`expiresAt` pəncərəsi və consume state-i: `(ConsumedAtUtc,
  ResponseId)` cütü schema səviyyəsində ya **hər ikisi boş** (pending), ya **hər
  ikisi dolu** (consumed) olmalıdır — yarım-consumed sətir yaradıla bilməz — və
  `ResponseId` ledger boyu unique-dir.

Raw nonce, raw artifact və signature dəyəri bu ledger-də saxlanmır.

### Validity window və single use

Recovery artifact müddət qaydası bu spec-də sahiblənir. `ADR-0039`-un 24 saatlıq
qərarı `ADR-0058` v2 protokolu altında qüvvədə qalır və
[`docs/01_PRODUCT_SCOPE.md`](../01_PRODUCT_SCOPE.md) § `Owner Recovery / Reset
Password` product-level ifadəsi ilə eyni pəncərəni bildirir:

- vendor-signed response artifact-ının default və **maximum** pəncərəsi
  `expiresAt − issuedAt ≤ 24 saat`-dır. Host bu üst həddi artifact-dakı dəyərə
  güvənmədən özü tətbiq edir: daha uzun pəncərə düzgün imzalanmış artifact-la da
  gətirilə bilər, ona görə signature keçsə belə import `invalid-validity-window`
  ilə rədd olunur;
- deployment config pəncərəni qısalda bilər, uzada bilməz. `expiresAt ≤ issuedAt`
  və host clock-skew tolerance-ından kənar future-dated `issuedAt` də
  `invalid-validity-window`-dur;
- artifact **single-use**-dır: uğurlu import nonce-u burn edir və consumed-nonce
  ledger-i müddətsiz saxlanır, yəni expiry-nin keçməsi nonce-u yenidən istifadəyə
  açmır ([`data-retention.md`](./data-retention.md));
- local pending request record-unun `expiresAt`-i out-of-band turnaround üçündür:
  default `issuedAt + 7 gün`, config qısalda bilər, amma response pəncərəsindən
  (24 saat) aşağı düşməz. Pending pəncərə keçibsə response `binding-mismatch`
  kimi rədd olunur və istifadəçi yeni request export edir; pending record-un
  silinməsi consumed-nonce tarixini silmir;
- həm pending request, həm response pəncərəsi trusted time-a qarşı yoxlanılır;
  vaxt state-i etibarlı deyilsə nəticə `trusted-time-unavailable` olur və heç bir
  credential mutation baş vermir.

Bu pəncərə P2 cross-device restore request pəncərəsi ilə eyni deyil; onun
lifecycle-i [`backup-restore-migration.md`](./backup-restore-migration.md)
tərəfindən sahiblənir.

### Response artifact və import ardıcıllığı

`.dobbyrecovery` canonical shape-i
[`dobby-recovery-response-v2.schema.json`](../schemas/dobby-recovery-response-v2.schema.json)
tərəfindən sahiblənir: `schemaVersion: 2`,
`purpose: dobby.owner-recovery-response`, `keyId`, `responseId`, `requestId`,
`nonce`, `customerId`, `deploymentId`, `deviceId`, `recoveryPublicKeyHash`,
`action: reset-owner-password`, `issuedAt`, `expiresAt` və purpose-specific vendor
signature.

Import ardıcıllığı məcburidir və fail-closed-dır:

1. artifact byte length, UTF-8 JSON parse və string/collection/timestamp bound-ları
   **allocation-dan əvvəl** yoxlanılır; limit aşılırsa parse davam etmir;
2. `schemaVersion`, `purpose`, `action` və exact `{ payload, signature }` shape-i
   yoxlanılır;
3. `signature.keyId` **exact** olaraq `payload.keyId` ilə eyni olmalıdır. JSON
   Schema bu cross-field bərabərliyi ifadə edə bilmir, ona görə qayda burada
   normativdir və host onu schema validation-dan asılı olmayaraq tətbiq edir:
   split key identity (`payload.keyId` trusted, `signature.keyId` başqa dəyər)
   `invalid-artifact` ilə rədd olunur, çünki key-i bir member-dən seçib
   authority-ni digərindən götürən verifier substituted key identity qəbul
   edərdi (`ADR-0062` `keyId` selector);
4. vendor signature purpose-specific recovery-response key və versioned `keyId`
   ilə verify olunur; imza keçmədən payload məzmununa etibar edilmir və license,
   plugin-package və ya setup-authorization key-i qəbul edilmir. Selector-un
   `recovery-<prod|test>-<YYYY-MM>` namespace-ində olması owning schema tərəfindən
   signature verification-dan əvvəl tələb olunur (`ADR-0062` § 1), amma format
   uyğunluğu trust yaratmır: key mütləq host recovery ring-ində exact match
   tapmalıdır;
5. `recoveryPublicKeyHash` cari device recovery public key-in hash-ı ilə eyni
   olmalıdır;
6. `requestId`, `nonce`, `customerId`, `deploymentId`, `deviceId` pending local
   request ilə **exact** uyğun gəlməlidir; qismən uyğunluq qəbul edilmir. Eyni
   exact müqayisə `payload.keyId` ↔ pending request-in `vendorKeyId` cütü üçün də
   məcburidir: cihaz hansı vendor recovery key-ini gözlədiyini request-də pin
   edir, ona görə rotation ring-də hələ trusted olan **başqa** key ilə imzalanmış
   response da `binding-mismatch` ilə rədd olunur. Bundan əlavə `customerId`,
   `deploymentId`, `deviceId` və `recoveryPublicKeyHash` cari runtime authority
   context-i ilə də yoxlanılır, yəni köhnə pending record başqa cihaz/deployment
   üçün istifadə edilə bilmir;
6a. **Cihaz materialının recovery ortasında dəyişməsi.** Recovery binding-i
   `MachineGuid`-ə deyil, device recovery key cütünə və pending record-a bağlıdır
   (`ADR-0058`; `ADR-0063` § 5). Ona görə request export edildikdən sonra `MachineGuid`
   dəyişsə (registry manipulyasiyası, OS reinstall) recovery import-un özü bundan
   asılı deyil: addım 5 və 6 keçirsə import davam edir. Əksinə, DPAPI ilə qorunan
   material itirilibsə device recovery private key-i unprotect olunmur, addım 5 fail
   olur və nəticə `binding-mismatch`-dir — heç bir override, yeni key generasiyası və
   ya "force import" yolu yoxdur. Bu halda yol recovery deyil, `ADR-0063` § 5 device
   replacement-dir. License device binding-in `MachineGuid` dəyişikliyinə reaksiyası
   ayrı məsələdir və `ADR-0063` § 4-də yazılıb; ikisi eyni binding deyil.

7. müddət yoxlanışı trusted time-a qarşı aparılır: `expiresAt > now`,
   `expiresAt − issuedAt ≤ 24 saat` host-enforced maximum-u və pending request
   pəncərəsinin hələ açıq olması. Vaxt state-i sağlam deyilsə import rədd edilir;
8. replay yoxlanışı request ledger-i üzərində aparılır və üç şərtdən hər biri
   müstəqil replay-dir: (a) pending request record-u artıq consumed işarələnib;
   (b) həmin `responseId` ledger-də hər hansı digər request üçün qeyd olunub
   (global uniqueness, yalnız cari sətir üzrə yoxlama deyil); (c) response
   nonce-unun SHA-256 hash-ı gözlənilən pending `NonceHash` ilə uyğun gəlmir.
   Müqayisələr həmişə hash üzərindədir, raw nonce müqayisə üçün saxlanmır. Bir
   response artifact-ı iki dəfə, iki fərqli request üçün, və ya iki paralel
   import cəhdində istifadə oluna bilməz. Ayrıca burned-nonce ledger-i
   (`ConsumedRecoveryNonces`) defense-in-depth qatıdır: onun read enforcement
   point-i snapshot promotion carry-forward-dur, yəni köhnə snapshot-un promote
   edilməsi artıq burn olunmuş nonce üçün pending record dirildə bilmir
   ([`storage-and-data-protection.md`](./storage-and-data-protection.md));
9. local Owner invariant yoxlanılır: Owner yoxdursa və ya birdən çox Owner
   invariantı pozulubsa import fail-closed rədd edilir;
10. bütün yoxlamalar keçdikdən sonra Owner credential reset, nonce burn, həmin
    user-in active session-larının invalidation-ı, remembered-account slot
    təmizlənməsi və audit evidence **bir atomik unit of work** kimi commit olunur
    (`ADR-0061`); commit uğursuz olarsa nonce consumed sayılmır, Owner credential
    dəyişmir və success publish olunmur. TOCTOU-ya qarşı iki əlavə tələb
    məcburidir: (a) 5–9 addımlarındakı binding/replay match-i mutation
    transaction-ının **içində yenidən** icra olunur — dialog açıq qalarkən dəyişən
    state ilə commit edilə bilməz; (b) burn əməliyyatı unconditional `UPDATE`
    deyil, hələ pending olmasını tələb edən conditional single-row update-dir
    (`WHERE RequestId = @RequestId AND ConsumedAtUtc IS NULL AND ResponseId IS
    NULL`) və təsirlənən sətir sayı **düz 1** olmalıdır; 0 olarsa nəticə replay
    kimi fail-closed rədd edilir, yəni iki paralel import-un ikisi də success
    görə bilməz.

User reset dialogunu ləğv edərsə nəticə typed cancelled olur: nonce consumed
edilmir, Owner credential və session state dəyişmir, artifact expiry-ə qədər
yenidən istifadə oluna bilər.

Uğursuz import mövcud Owner credential-ını, session state-ini və throttle
ledger-ini korlamır; recovery cəhdləri də durable throttle ledger-inə yazılır.

### Recovery reject vocabulary

Reject səbəbləri user-facing səthdə typed qalır, amma raw security detail
sızdırmır:

| Səbəb | User-facing məna |
|---|---|
| `invalid-recovery-file` | Fayl Dobby recovery artifact-ı deyil. |
| `unsupported-recovery-version` | Faylın versiyası bu host tərəfindən dəstəklənmir. |
| `artifact-limit-exceeded` | Fayl gözlənilən ölçü/dəyər hədlərini aşır. |
| `invalid-signature` | Artifact doğrulanmadı. |
| `untrusted-vendor` | Artifact trusted vendor recovery key-i ilə imzalanmayıb. |
| `binding-mismatch` | Artifact bu cihaz/deployment/request üçün keçərli deyil. |
| `expired-artifact` | Artifact-ın müddəti bitib. |
| `invalid-validity-window` | Artifact-ın müddət pəncərəsi qəbul edilən hədlərdən kənardır. |
| `replayed-nonce` | Bu recovery artifact-ı artıq istifadə olunub. |
| `owner-invariant-violated` | Local Owner vəziyyəti bu əməliyyata icazə vermir. |
| `trusted-time-unavailable` | Cihaz vaxtı etibarlı deyil; artifact yoxlanıla bilmədi. |
| `recovery-failed` | Əməliyyat tamamlanmadı; əvvəlki credential vəziyyəti dəyişməyib. |

Konkret `customerId`, `deploymentId`, `deviceId`, `requestId`, nonce, key id,
local path, raw signature və exception detail-i user-facing mesajda göstərilmir.

### Recovery audit

Recovery audit event adları `docs/specs/audit-and-diagnostics.md` § `Audit Event
Taxonomy` closed registry-sindən gəlir; bu spec ayrı ad icad etmir. Minimum
audit event-lər:

| Recovery addımı | Canonical event | `result` | Ayırd edici detail |
|---|---|---|---|
| Request artifact export olunur | `recovery.export` | `success` | `requestId` |
| Response import uğurla tamamlanır | `recovery.import` | `success` | `outcome`, `requestId` |
| Response import typed reject alır | `recovery.import` | `denied` | `outcome` (typed reject səbəbi), `requestId` |

Import cəhdinin nəticəsi ayrı event adı ilə deyil, **eyni** `recovery.import`
adının `result` + `outcome` cütü ilə ifadə olunur: reject səbəbi
`Recovery reject vocabulary` cədvəlindəki typed dəyərdir. Ayrıca
`owner.recovery.*` adlı event mövcud deyil və yaradılmır — closed registry-dən
kənar ad audit consumer-lərini (support sorğusu, diagnostics export, retention)
yoxlanılmaz vəziyyətə salır.

Audit qeydi yalnız audit-safe field-lərlə məhdudlaşır: action id, actor context,
typed reason code, `requestId`, `nonceHash` (non-reversible) və correlation id.
Bu field-lər `Dobby.Audit.AuditDetailsSanitizer` write-time allow-list-ində
qeydiyyatdan keçmiş olmalıdır: allow-list-də olmayan field emission zamanı
fail-closed atılır, ona görə sənəddə "icazəli" göstərilən field allow-list-də də
mövcud olmalıdır. Raw nonce, raw artifact, signature value, key material və stack
trace audit-ə və exported diagnostics-ə düşmür (`ADR-0019`, `NFR-026`). Pre-auth
recovery audit qeydləri `ADR-0061` closed pre-auth audit set-inə daxildir.

## Active User Deactivation

Managed user deactivate ediləndə həmin user üçün future login deny edilir. Əgər həmin user-in active session-ı varsa, session növbəti authorization-required əməliyyatdan gec olmayaraq invalid edilir və protected əməliyyat davam etmir.

Invalidated session user-i login ekranına qaytarır və session invalidation audit trail-ə düşür.

## Work Session Davranışı

Logout zamanı active work session varsa modal warning göstərilir. Təsdiqdən sonra:

- work session-lar close/cancel edilir;
- process-lifetime state təmizlənir;
- login ekranı açılır.

Lock/manual lock/auto-lock work session-ları close etmir. Unlock sonrası session-lar process memory-də qaldığı yerdən görünə bilər.

Minimize behavior, minimized strip metadata, navigation warning və restart boundary `docs/specs/work-sessions.md` ilə bağlanır.

## Audit

Minimum audit event-lər:

- `user.login`;
- `user.login.failed`;
- `user.logout`;
- `user.password.change`;
- `user.password.reset`;
- `session.lock`;
- `session.unlock`;
- `session.expired`;
- `session.invalidated`.

Owner recovery event-ləri `Recovery audit` bölməsindədir. Login/unlock throttle
deny-ləri `user.login.failed` altında typed reason ilə qeyd olunur və account-ın
mövcudluğunu açıqlamır.

Diagnostics detallarını audit payload-u ilə qarışdırmaq olmaz; boundary `ADR-0019`-a tabedir.

## Acceptance Hooks

- `AC-001` pre-auth allow-list və session-commit-before-scope davranışını yoxlayır.
- `AC-026` durable throttle bucket-larını və restart davranışını yoxlayır.
- `AC-028` logout cleanup, remembered-login interaction və single active session behavior-u yoxlayır.
- `AC-029` password change, manual lock, auto-lock və lock screen behavior-u yoxlayır.
- `AC-019` work session minimize/restore, no-auto-minimize warning və restart boundary-ni yoxlayır.

## Branch Shared Session Addendum

Standalone session lifecycle remains local-first.

Branch Shared login/session creation requires Branch Shared authority to be
reachable and validated. The app validates server identity, `branchDeploymentId`,
schema/grants and credential authority before creating an authenticated shared
session.

If Branch Shared authority becomes unavailable during a session, protected
shared commands fail closed until authority is reachable and revalidated. The UI
may preserve in-memory state and show retry/reconnect status, but this does not
authorize new shared writes.

Initial reconnect policy values are owned by `ADR-0056` /
`docs/specs/postgres-deployment-ops.md` and remain Tier B placeholders until
runtime implementation opens.

## Verification Obligations

Bu spec-in tələbləri behavior-first sübutla bağlanır (`ADR-0061`, `NFR-027`).
Source-text, regex, fayl mövcudluğu yoxlaması və ya "test list green" iddiası
aşağıdakı maddələri qapatmır:

| Yoxlama | Minimum sübut |
|---|---|
| Remembered login secret saxlamır | Login → proses restart testi: persisted slot-da password/verifier/reversible material yoxdur, password field boş açılır, login yenidən tam credential verification tələb edir |
| Durable throttle | Deterministik `TimeProvider` seam-i ilə restart-across test: qalan cəza bərpa olunur; account bucket-i ilə device/global bucket müstəqil davranır; username dəyişmək device/global cəzanı sıfırlamır |
| Timing indistinguishability | Naməlum və mövcud account üçün eyni user-facing nəticə/timing davranışını göstərən test; sleep-based ölçmə evidence deyil |
| Bounded verification | Concurrent login cəhdlərinin `ResourceBusy` verdiyini və warm-up-un cancellable/one-shot olduğunu göstərən test |
| Password change proof | Yanlış current password ilə non-disclosing deny + throttle qeydi; service boundary-də UI bypass cəhdinin rədd edilməsi |
| Session commit sırası | Behavior test: authenticated scope yalnız session commit-dən sonra yaradılır və logout-da dispose olunur |
| Atomicity | Fault injection: credential/session/audit/outbox commit-i uğursuz olanda köhnə state qalır və success publish olunmur |
| Recovery signature/purpose | Yanlış `purpose`, yanlış `keyId`, license/plugin-package key-i ilə imzalanmış response və encoding variant (padded base64, DER) fixture-larının fail-closed olması |
| Recovery binding | `requestId`/`nonce`/`deviceId`/`recoveryPublicKeyHash` uyğunsuzluğu və qismən uyğunluq fixture-larının rədd edilməsi |
| Recovery validity window | Düzgün imzalanmış, lakin `expiresAt − issuedAt > 24 saat` olan fixture-un `invalid-validity-window` ilə rədd edilməsi; expired pending request üzərində gələn response-un rədd edilməsi; deterministik `TimeProvider` seam-i ilə expiry keçidi (sleep evidence deyil) |
| Recovery replay | Eyni nonce ilə ikinci import cəhdinin `replayed-nonce` verməsi və commit uğursuzluğunda nonce-un consumed sayılmaması. Ayrıca üç müstəqil branch tələb olunur: consumed pending record, başqa request üçün qeyd olunmuş `responseId` (global uniqueness) və pending `NonceHash` ilə uyğun gəlməyən nonce |
| Recovery concurrency (TOCTOU) | İki paralel import cəhdinin **yalnız birinin** success verdiyini göstərən test: burn conditional single-row update-dir və 0 affected row halında nəticə fail-closed olur; həmçinin verification-dan sonra, commit-dən əvvəl pending record-un dəyişdirildiyi fixture-da import rədd olunur (match mutation transaction-ı içində yenidən icra olunur). Kod oxunuşu və ya "transaction istifadə olunur" iddiası bu sətri qapatmır |
| Recovery pending state integrity | Persist olunmuş pending record-un binding sütunlarının və ya `PayloadHash`-ının kənardan dəyişdirildiyi fixture-da import-un `binding-mismatch` ilə rədd edilməsi; pending ledger-in heç bir sətrində raw nonce olmadığını göstərən byte/field yoxlaması |
| Recovery cancellation | Cancel-də typed cancelled nəticə, dəyişməmiş credential/session state və consumed olmayan nonce |
| Lock concealment | Runtime UI testi: lock screen aktiv olanda authenticated shell content-i render olunmur (visibility ilə ölçülür), lock screen özü görünür qalır və unlock/return sonrası shell content əvvəlki görünüşünə bərpa olunur; yalnız input bloklanması bu maddəni qapatmır |
| Lock bypass | Lock screen-i unlock etmədən bağlamaq (pəncərə close/cancel) cəhdinin authenticated shell-ə qaytarmadığını, login ekranına düşdüyünü göstərən runtime test |
| Recovery audit contract | Real export və import (success, typed reject, cancel) axınlarından sonra audit trail-də yalnız `recovery.export`/`recovery.import` adlarının göründüyünü, `outcome` + `result` cütünün nəticəni ayırd etdiyini və `nonceHash` dəyərinin `RecoveryRequests.NonceHash` sütunu ilə eyni olduğunu göstərən test; closed registry-dən kənar ad emission-ın fail-closed atıldığı da göstərilir |
| Non-disclosure | User-facing mesajlarda konkret id, nonce, key id, path, signature və exception detail-in olmaması |

## Cross-references

- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-001`, `FR-021`, `FR-022`, `FR-023`, `FR-024`, `FR-031`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-002`, `NFR-025`, `NFR-026`, `NFR-027`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-001`, `AC-019`, `AC-026`, `AC-028`, `AC-029`
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — controlling credential/throttle/recovery decision
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — pre-auth boundary, atomic mutation, behavior-first proof
- `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` — session/lock semantics
- `docs/adr/ADR-0038-local-first-authentication.md` — local-first auth (saved-credential və throttle hissələri `ADR-0058` ilə əvəz olunub)
- `docs/adr/ADR-0039-owner-password-recovery.md` — recovery v1 tarixi qeyd (hardware-quorum modeli `ADR-0058` ilə əvəz olunub)
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` — protected key material scope
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` — audit/diagnostics redaction boundary
- `docs/schemas/dobby-recovery-request-v2.schema.json` — recovery request artifact
- `docs/schemas/dobby-recovery-response-v2.schema.json` — recovery response artifact
- `docs/specs/storage-and-data-protection.md` — pre-login closed table set, remembered-login və throttle persistence
- `docs/specs/user-management.md` — managed user reset və Owner guard boundary
- `docs/specs/admin-tool.md` — vendor-side `create-recovery-response` command
- `docs/specs/work-sessions.md` — work session lifecycle
- `docs/specs/data-retention.md` — throttle/nonce retention floor-ları

## Revision History

- **2026-08-27:** Recovery replay/binding modeli sənədə gətirildi. Əvvəlki mətn
  pending request state-ini "`requestId`/`nonce`/`expiresAt` local olaraq
  saxlanılır" kimi təsvir edirdi — bu, hansı state-in sahibləndiyini
  göstərmirdi və oxucunu **raw nonce persist etməyə** yönləndirirdi, halbuki
  yalnız 64-hex SHA-256 `NonceHash` saxlanılır. Artıq `RecoveryRequests` ledger-i
  və onun invariantları (globally unique `NonceHash`, unique `ResponseId`,
  `(ConsumedAtUtc, ResponseId)` paired-null, reconstruct-and-compare `PayloadHash`)
  explicit-dir. Import ardıcıllığının 8-ci addımı primary gate-i ledger kimi
  göstərir və `ConsumedRecoveryNonces`-i öz read enforcement point-i (snapshot
  promotion carry-forward) ilə defense-in-depth qatı kimi düzgün yerləşdirir;
  əvvəl sıralama tərsinə idi. 10-cu addıma iki TOCTOU tələbi əlavə olundu:
  binding/replay match-in mutation transaction-ı içində **yenidən** icrası və
  burn-un `ConsumedAtUtc IS NULL AND ResponseId IS NULL` şərtli, "affected rows
  düz 1" tələbli single-row update olması — əvvəl paralel iki import-un ikisinin
  də success görməməsi qaydası yalnız nəticə kimi bəyan edilirdi, mexanizmi
  yazılmırdı. Remembered-login legacy migration bullet-i də konkretləşdirildi:
  reconcile olunmayan slot bərpa edilmir və canonical qayda
  `docs/specs/storage-and-data-protection.md` § `Remembered-login slot legacy
  migration (owning rule)`-dadır.

- **2026-08-27:** Lock concealment qaydası əlavə edildi. Əvvəlki mətn lock-un
  session-u saxladığını yazırdı, lakin locked vəziyyətdə authenticated shell
  content-inin görünməməsini tələb etmirdi; nəzarətsiz workstation-da minimized
  work strip label-ları, plugin page məzmunu və device code lock screen-in
  arxasında oxunaqlı qalırdı. Lock screen-də göstərilən identity minimuma
  bağlandı və `Lock concealment` + `Lock bypass` verification obligation sətirləri
  əlavə edildi.
- **2026-08-26:** `Validity window və single use` bölməsi əlavə edildi. Recovery
  artifact müddət pəncərəsinin canonical sahibi bu spec oldu (24 saatlıq default
  + host-enforced maximum, pending request pəncərəsi, single-use nonce
  semantikası); import ardıcıllığının 6-cı addımı, `invalid-validity-window`
  reject səbəbi və uyğun verification obligation sətri əlavə edildi. Əvvəl bu
  ədəd yalnız `docs/01` və superseded `ADR-0039`-da qalırdı.
- **2026-08-25:** v2 reconciliation. Remembered-login masked-password mətni
  silindi və `ADR-0058` no-secret modeli ilə əvəz olundu; durable throttle
  bölməsi, session-commit-before-scope sırası, password-change proof, Owner
  Recovery v2 (device key material, request/response artifact-ları, import
  ardıcıllığı, reject vocabulary, audit) və behavior-first Verification
  Obligations əlavə edildi; `2026-08 Hardening Amendment` bölməsi ləğv edildi,
  çünki qərarları normativ mətnə köçürüldü.
