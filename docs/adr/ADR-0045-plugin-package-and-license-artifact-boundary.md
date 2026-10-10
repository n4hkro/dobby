# ADR-0045: Plugin Package and License Artifact Boundary

**Status:** Accepted (signature format and license lifecycle details amended by ADR-0059/ADR-0060 — DER signature encoding is legacy; successor artifacts use `IeeeP1363FixedFieldConcatenation` base64url-no-padding signatures and license v3 intent semantics; the package/license artifact boundary itself remains binding)
**Date:** 2026-04-25

## Context

`ADR-0032` plugin package signature trust modelini bağlayır və `ADR-0036` manifest schema-da `signature` field-i göstərir. Bununla belə package layout, detached license artifact boundary, deterministic hash/signature placement və forbidden package content qaydaları kifayət qədər explicit deyildi.

Bu ADR `ADR-0032`-ni supersede etmir; signature trust modelini saxlayır və package formatının P1 canonical placement/verification detalını dəqiqləşdirir.

## Decision

Plugin installation və plugin activation ayrı artefaktlardır.

- Install artefakt vendor-created signed `.dobbyplugin` package-dir.
- P1 install source local file picker ilə seçilən `.dobbyplugin` faylıdır; remote
  marketplace/catalog install P1 scope-una daxil deyil.
- Activation/license artefakt vendor-signed detached `.dobbylicense` faylıdır və plugin install package-inə embed edilmir.
- Plugin package `.dobbylicense`, customer-specific license payload, customer data, device fingerprint, vendor private key, host internal configuration və host persistence migration script daşımır.
- Plugin package və license artifact ayrıca trust obyektləridir; install zamanı package signature, license import zamanı isə license signature/binding/fingerprint yoxlanılır.

### License artifact boundary

`.dobbylicense` Dobby host tərəfindən import olunan detached license artifact-dır. Bu artifact:

- vendor signing key ilə imzalanır;
- package-ə embed edilmir və `.dobbyplugin` içindən çıxarılmır;
- plugin, customer, deployment, device və package fingerprint binding məlumatlarını daşıyır;
- activation/not-before və expiry məlumatlarını daşıyır;
- plugin-ə raw JSON/detail kimi verilmir.

License artifact outer `{ payload, signature }` wire formatı, payload semantics və
lifecycle semantikası `ADR-0060`, customer/deployment binding isə `ADR-0052` ilə
bağlanır. Bu ADR `.dobbylicense` signature field placement-i üçün owner deyil.

### Canonical signature model

P1-də plugin manifest-in içində `signature` block-u saxlanılır. Default package ayrıca `signature.bin` yaratmır.
Bu ADR signature block-unun **yerləşməsi** (manifest daxilində, detached fayl deyil),
signed payload-ın hansı content üzərində qurulduğu, deterministic `files` section
və package fingerprint üçün canonical owner-dir. Signature block-unun **field
shape-i** `docs/schemas/dobby-plugin-manifest-v2.schema.json` tərəfindən,
signature format/encoding və key purpose separation isə `ADR-0059`
§ `Signatures, fingerprint, and keys` tərəfindən idarə olunur; `ADR-0032` yalnız
ECDSA/trust verification semantics-ini saxlayır. Bu ADR həmin iki authority-ni
override etmir.

Manifest `signature` block-unun canonical inner shape-i (`schemaVersion = 2`):

```json
{
  "purpose": "dobby.plugin-package",
  "algorithm": "ECDSA-P256-SHA256",
  "format": "IeeeP1363FixedFieldConcatenation",
  "keyId": "plugin-prod-2026-01",
  "value": "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyAhIiMkJSYnKCkqKywtLi8wMTIzNDU2Nzg5Ojs8PT4_QA"
}
```

- Block `additionalProperties: false`-dır: yuxarıdakı beş field-in hamısı tələb
  olunur və naməlum field manifest validation failure-dır. Numeric `keyVersion`
  field-i **qəbul edilmir** — o, `ADR-0062` ilə retired edilmiş selector-dur və
  v2 schema onu unknown property kimi fail-closed rədd edir.
- `purpose` yalnız `dobby.plugin-package` dəyərini qəbul edir. Başqa purpose
  dəyəri (məs. `dobby.plugin-license`) manifest signature block-unda key-purpose
  confusion cəhdidir və rədd olunur (`ADR-0059`).
- `algorithm` yalnız `ECDSA-P256-SHA256` dəyərini qəbul edir; başqa algorithm
  dəyəri Phase 1-də validation failure-dır.
- `format` yalnız `IeeeP1363FixedFieldConcatenation` dəyərini qəbul edir və
  verification bu format-ı **explicit** ötürür; provider default-una güvənilmir.
  DER/ASN.1 sequence encoding P1 manifest signature value kimi qəbul edilmir —
  iki encoding-in eyni vaxtda qəbul edilməsi signature encoding ambiguity yaradır.
- `keyId` verification üçün istifadə olunan purpose-separated leaf key-in canonical
  string selector-udur (`<purpose-short>-<prod|test>-<YYYY-MM>`, məs.
  `plugin-prod-2026-01`). Seçim exact match ilə aparılır; naməlum və ya retired
  `keyId` fail-closed rədd olunur və "bütün açarları sına" davranışı qadağandır
  (`ADR-0062` § 4). Production və dev/QA trust profile-ları bu field-i raw private
  key materialı sızdırmadan interpret edir.
- `value` canonical signed payload üzərində hesablanmış ECDSA signature-ın
  `IeeeP1363FixedFieldConcatenation` (fixed-field `r ‖ s`, P-256 üçün 64 bayt)
  formasının base64url, no-padding kodlaşdırılmasıdır — sabit 86 simvol. Hex,
  standard padded base64, DER bytes və file path reference P1 manifest signature
  value kimi qəbul edilmir.

Signed payload:

- canonical manifest content with `signature` block removed;
- manifest içində deterministic `files` section serialization (`path`, `sha256`);
- package fingerprint canonical signed content əsasında hesablanır.

Manifest signed-payload canonicalization qaydaları:

- input UTF-8-dir və BOM yoxdur;
- `signature` block-u canonical payload-dan tam çıxarılır;
- object property names every nested object level-də ordinal/culture-invariant
  ascending sortlanır;
- arrays declared order-i qoruyur; `extensionPoints` order-i ADR-0036 və
  ADR-0005 tiebreak semantics-i üçün dəyişdirilmir;
- insignificant whitespace yoxdur;
- strings NFC-normalized edilir;
- numeric JSON value-lər string wrapper-ə çevrilmir;
- `files` section path-a görə ordinal/culture-invariant ascending order-da
  canonical serialization-a daxil edilir.

`signature.bin` yalnız gələcək compatibility/debug ADR-i explicit açarsa qəbul edilə bilər; P1 canonical package formatında yoxdur.

### File hash manifest placement

P1-də file hash manifest plugin manifest içində deterministic `files` section kimi saxlanılır:

```json
[
  { "path": "<canonical-path>", "sha256": "<lowercase-hex-sha256>" }
]
```

- `files` section path-a görə ordinal, culture-invariant ascending sortlanır.
- Ayrı `package-hashes.json` artefaktı yaradılmır.
- Manifest faylının özü `files` entry deyil; manifest content canonical signing payload tərəfindən cover olunur.

### Completeness and reject rules

Verifier-in tolerance-i yoxdur:

- non-manifest file package-də var, amma `files` section-da yoxdur = REJECT;
- `files` section-da var, amma package-də yoxdur = REJECT;
- package-də var və `files` section-da var, amma hash mismatch = REJECT;
- manifest content canonical signing payload ilə uyğun gəlmir = REJECT;
- host persistence migration script, customer data, device fingerprint, vendor private key və ya host internal config tapılır = REJECT.

Zero-byte file allowed-dur və SHA-256 empty hash ilə `files` section-da yer alır. Symlink P1-də forbidden-dur və tapılarsa package reject edilir.

Install validation failure user-facing mesajı faydalı typed səbəb göstərməlidir
(`invalid-manifest`, `signature-failed`, `trust-failed`, `forbidden-content`,
`hash-mismatch` və s.), amma raw key material, full path disclosure və ya internal
verification stack detail-i UI-da göstərməməlidir. Detallar diagnostics/audit
boundary-yə tabedir.

### Path canonicalization

Canonical path UTF-8 + NFC normalization-dan sonra yalnız `/` separator istifadə edir. Verifier ardıcıl pipeline işlətməlidir:

1. UTF-8 decode və NFC unicode normalization.
2. `\` separator-unun `/`-ə çevrilməsi.
3. Forbidden path checks: `..`, absolute/root path, drive-letter path, UNC/device path, ADS, reserved Windows device names, leading/trailing/empty segment, control characters.
4. Duplicate/case-collision checks həm manifest entries, həm actual package container entries üzərində.
5. Length/depth limits: segment > 255 char və depth > 32 reject edilir.

Directory marker entries Phase 1-də dəstəklənmir; bütün entries file entry-dir.

### Package fingerprint

Package fingerprint raw zip/container bytes deyil. Fingerprint canonical signed content üzərindən hesablanır:

```text
SHA-256(canonical manifest signing payload + deterministic files section serialization)
```

Compression level, zip entry timestamp, container ordering metadata və container-specific metadata package identity hesablanmasına daxil deyil.

## Consequences

- Package verification deterministic və cross-platform repeatable olur.
- License/package binding raw zip bytes-a yox, canonical signed content fingerprint-ə bağlanır.
- Host persistence migration ownership `ADR-0030` sərhədində qalır və plugin package boundary-sinə sızmır.
- `ADR-0032` signature trust primitive-ləri saxlanır, amma P1 package placement manifest `signature` block-u kimi dəqiqləşir.
- `ADR-0036` manifest schema `signature` və `files` section semantics-ini bu ADR-ə istinadla implement etməlidir.
- `.dobbyplugin` və `.dobbylicense` support/debug zamanı ayrıca yoxlanılan artifact sinifləri kimi qalır.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — Plugins və Licensing, package/activation boundary
- `docs/02_ARCHITECTURE_MAP.md` — Row 27 `Plugin Packaging`, Row 38 `Plugin Trust Verification`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — Plugin packaging/signature stack rows
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-007`, `FR-014`, `FR-025`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-014`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-004`, `AC-018`, `AC-031`
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md`
- `docs/adr/ADR-0032-plugin-signature-trust-model.md`
- `docs/adr/ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md`
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md`
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — signature format,
  domain separation və key purpose separation authority-si
- `docs/schemas/dobby-plugin-manifest-v2.schema.json` — signature block field shape
- `docs/specs/plugins-manager.md`

## Revision History

- **2026-08-27:** Factual reconciliation of § `Canonical signature model` against
  its owning authorities; qərar dəyişikliyi deyil. Bu Accepted ADR özünü signature
  shape üçün "yeganə canonical owner" elan edir və nümunə blok kimi
  `{ algorithm, keyVersion: 1, value }` göstərirdi, `value`-nu isə "DER-encoded
  ECDSA signature bytes" kimi təsvir edirdi. Hər üç iddia HEAD-də enforce olunan
  davranışla ziddiyyətdə idi: `docs/schemas/dobby-plugin-manifest-v2.schema.json`
  `signature` block-unda `purpose`, `algorithm`, `format`, `keyId`, `value`
  field-lərini tələb edir, `additionalProperties: false` ilə numeric `keyVersion`-u
  unknown property kimi rədd edir; `ADR-0059` (2026-08-21) signature format-ını
  `IeeeP1363FixedFieldConcatenation` kimi bağlayır və kod
  (`src/Dobby.Security/Trust/ArtifactSignatureProfile.cs`,
  `src/Dobby.PluginFramework/Manifest/PluginManifestContract.cs`) həmin format-ı explicit
  ötürür. DER təsviri xüsusilə təhlükəli idi: iki fərqli signature encoding-in
  legitimləşdirilməsi signature encoding ambiguity yaradır. Mətn schema/`ADR-0059`
  reallığına uyğunlaşdırıldı, ownership claim-i placement/signed-payload/fingerprint
  ilə məhdudlaşdırıldı və `keyId` exact-match seçimi `ADR-0062` § 4-ə bağlandı.
  Package layout, license artifact boundary, path canonicalization və fingerprint
  qərarları dəyişmir, ona görə successor ADR yaradılmır.
