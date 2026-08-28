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
lifecycle semantikası `ADR-0051`, customer/deployment binding isə `ADR-0052` ilə
bağlanır. Bu ADR `.dobbylicense` signature field placement-i üçün owner deyil.

### Canonical signature model

P1-də plugin manifest-in içində `signature` block-u saxlanılır. Default package ayrıca `signature.bin` yaratmır.
Bu ADR signed payload shape, manifest signature placement, deterministic `files`
section və package fingerprint üçün yeganə canonical owner-dir; `ADR-0032` yalnız
signature algorithm/trust verification semantics-ini saxlayır.

Manifest `signature` block-unun P1 inner shape-i:

```json
{
  "algorithm": "ECDSA-P256-SHA256",
  "keyVersion": 1,
  "value": "MEQCIAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gAiAhIiMkJSYnKCkqKywtLi8wMTIzNDU2Nzg5Ojs8PT4_QA"
}
```

- `algorithm` yalnız `ECDSA-P256-SHA256` dəyərini qəbul edir; başqa algorithm
  dəyəri Phase 1-də validation failure-dır.
- `keyVersion` vendor trust materialının hansı public key version-u ilə verify
  olunacağını göstərən JSON number-dır. Phase 1 dəyəri `1`-dir; gələcək rotation
  dəyərləri `2`, `3`, ... kimi numeric artırılır və string `"v1"`/`"v2"` forması
  P1 manifest signature shape-ində qəbul edilmir. Production və dev/QA trust
  profile-ları bu field-i raw private key materialı sızdırmadan interpret edir.
- `value` canonical signed payload üzərində hesablanmış DER-encoded ECDSA signature
  bytes-ın base64url, no-padding formasıdır. Hex, standard padded base64 və file
  path reference P1 manifest signature value kimi qəbul edilmir.

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
- `docs/adr/ADR-0033-signing-key-ownership.md`
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
- `docs/adr/ADR-0051-license-lifecycle-v2.md` (superseded by `ADR-0060` license v3)
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`
- `docs/specs/plugins-manager.md`
