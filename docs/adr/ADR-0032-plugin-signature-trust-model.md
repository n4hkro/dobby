# ADR-0032: Plugin Signature Trust Model

**Status:** Accepted (trust-anchor identity and key-purpose rationale amended 2026-08-26 by `ADR-0062` — see `## 2026-08 Key Purpose Separation Amendment`)
**Date:** 2026-04-24

## Context

`01_PRODUCT_SCOPE.md` `## Plugins və Licensing` plugin installasiya artefaktının `vendor-admin tərəfindən hazırlanmış imzalanmış plugin paketi` olmasını tələb edir; `vendor, paket imzası və manifest/trust yoxlamaları` install zamanı məcburidir. `02_ARCHITECTURE_MAP.md` Cədvəl 3 `Plugin Signature Trust Model` sətri bunu Phase 1 blocker kimi göstərir və `unsigned və ya untrusted plugin activation-a buraxılmır` qaydasını açıq yazır.

`ADR-0036` manifest schema-sında `signature` field-i elan edir, amma imza formatı və verification axını burada bağlanmır. Bu ADR ilk yazılanda `ADR-0039` (indi `ADR-0058` ilə superseded) vendor master public key-in compile-time resource kimi embed olunmasını tələb edir və `plugin signature trust model ilə ortaq master key-dir` deyirdi. Həmin ortaq-master-key mövqeyi artıq authority deyil: key custody, purpose separation və anchor provisioning `ADR-0062`-nin öhdəliyindədir (aşağıdakı `2026-08 Key Purpose Separation Amendment`).

Bu ADR imza formatını, verification axınını və trust anchor qaydasını rəsmiləşdirir.

## Decision

Phase 1 plugin signature trust modeli:

### 1. İmza alqoritmi

- ECDSA P-256 üzərində imzalama.
- İmza canonical JSON serialization-ın SHA-256 hash-i üzərindən hesablanır.
- İmzalanan payload shape, plugin manifest `signature` block placement-i,
  deterministic `files` section serialization və package fingerprint semantics-i
  `ADR-0045` tərəfindən canonical idarə olunur.
Phase 1 signature placement is canonical in the plugin manifest `signature` block as defined by ADR-0045. Standalone `signature.bin` P1 canonical package formatında yoxdur; it may only be reconsidered by a future compatibility/debug ADR.

ECDSA P-256 **primitive**-i host-da recovery və license verification-ı ilə eynidir, lakin bu **key paylaşımı deyil**: hər signing purpose ayrı versioned leaf key daşıyır və bir purpose-un key-i başqa purpose-un artifact-ını verify etmək üçün qəbul edilmir (`ADR-0062` § 1, `ADR-0059`). Ortaq olan yalnız kriptoqrafik alqoritm və canonical-payload qaydasıdır; verification path key-purpose və `keyId` üzrə ayrılır.

### 2. Trust anchor

- Host binary build zamanı `dobby.plugin-package` purpose-unun vendor **public key**-i compile-time embedded resource kimi daxil edilir (`ADR-0062` § 3 canonical anchor cədvəli; production `vendor-plugin-package-public-key.pem`, dev/QA `dev-qa-plugin-package-public-key.pem`). Retired shared `vendor-master-public-key.pem` adı trust ring-ə daxil edilmir.
- Host runtime-da heç bir online trust anchor yoxlaması yoxdur; bütün verification offline-first işləyir.
- Trust anchor key material `Dobby.Security / Plugin Trust Verification` namespace-inin internal resource-udur; runtime-da filesystem və ya configuration-dan trust material yüklənmir.
- Purpose-separated vendor private leaf key custody, anchor provisioning, profile exclusivity və rotation ring `ADR-0062`-nin öhdəliyindədir və bu ADR-dən kənar saxlanır.

### 3. Verification axını

Plugin install və activation zamanı aşağıdakı ardıcıllıq məcburidir:

1. Plugin package oxunur və manifest parse olunur (`ADR-0036` schema uyğunluğu əvvəlcə yoxlanır).
2. Manifest-də `signature` field-i və `vendorId` field-i mövcudluğu yoxlanır; yoxdursa paket rədd olunur.
3. Paket daxilindəki bütün faylların SHA-256 hash-i yenidən hesablanır və ADR-0045
   deterministic `files` section-u ilə müqayisə olunur; uyğunsuzluq varsa paket
   rədd olunur (tamper detected).
4. ADR-0045 canonical signed payload üzərindəki imza artifact-ın daşıdığı `keyId`-ə
   uyğun `dobby.plugin-package` purpose key-i ilə ECDSA P-256 verify olunur;
   naməlum/retired `keyId`, başqa purpose-un key-i və uyğunsuz imza fail-closed
   rədd olunur (`ADR-0062` § 4 — ring boyu "try every key" davranışı qadağandır).
5. `vendorId` field host tərəfindən tanınan vendor-dursa (Phase 1-də yalnız master vendor qəbul olunur) paket trusted hesab olunur; əks halda rədd olunur.

Hər addımın nəticəsi `Dobby.Audit / Audit Trail` (Row 70) vasitəsilə qeyd olunur (`plugin.install.attempted`, `plugin.install.signature-failed`, `plugin.install.trust-failed`, `plugin.install.verified`).

### 4. Rejected alternatives

- **X.509 certificate chain:** Phase 1-də rədd edilir — offline-first sisteme CRL/OCSP yoxlaması uyğunlaşmır; trust store management skeleton Phase 1-in scope-undan kənardır.
- **RSA-2048 əvəzinə ECDSA:** ECDSA P-256 daha kiçik imza ölçüsü verir (~72 byte vs 256 byte) və host-un digər signed-artifact verification path-ları ilə eyni primitive-dir (key paylaşımı olmadan — `ADR-0062` § 1).
- **Self-signed per-customer trust:** Phase 1-də rədd edilir — single master vendor trust anchor deployment sadəliyi üçün seçilir; per-customer trust P2-də açıla bilər.

## Consequences

- Plugin install və activation signature + trust verify addımlarını məcburi keçir; unsigned və ya untrusted plugin activation-a buraxılmır.
- `Dobby.Security / Plugin Trust Verification` namespace-i (Row 38) bu ADR-i canonical qaydaları kimi götürür.
- `Dobby.PluginFramework / Plugin Packaging` namespace-i (Row 27) signature placement-i paket formatına daxil edir.
- `ADR-0036` manifest `signature` field-inin trust verification semantic-i bu ADR-də,
  canonical payload/package placement semantic-i isə `ADR-0045`-də dəqiqləşdirilir.
- Plugin-package trust anchor host binary-dan ayrıla bilmir; rotation yeni host build tələb edir və rotation protocol/ring authority-si `ADR-0062` § 4-dür (P1-də hər purpose üçün dəqiq bir current key).
- `ADR-0062` vendor private key custody-sini və public anchor provisioning-i bağlayır; bu ADR yalnız public side verification-ı sahib edir.

## 2026-08 Key Purpose Separation Amendment

**Tarix:** 2026-08-26 · **Authority:** `ADR-0062` (supersedes `ADR-0033`), `ADR-0058`, `ADR-0059`, `ADR-0060`

Bu ADR-in Decision-u (ECDSA P-256, canonical JSON SHA-256 payload, offline embedded anchor, məcburi verification ardıcıllığı) dəyişmir. Amendment yalnız artıq yanlış olan iki iddianı düzəldir:

1. **"Ortaq master key / tək verification path"** — əvvəlki mətn imza primitive-inin `ADR-0039` recovery imzası ilə paylaşılmasını "host-da iki ayrı verification path olmasın" arqumenti kimi göstərirdi. `ADR-0059`/`ADR-0062` bunu qadağan edir: `dobby.plugin-package`, `dobby.plugin-license` və `dobby.owner-recovery-response` ayrı purpose-lardır, ayrı leaf key və `keyId` daşıyır, key-purpose confusion fail-closed rədd olunur. Ortaq qalan yalnız alqoritmdir.
2. **"Vendor master public key"** — anchor adı və yolu retired-dir. Canonical anchor `ADR-0062` § 3 cədvəlindəki purpose-separated fayldır; `vendor-master-public-key.pem` adı trust ring-ə daxil edilmir.

Buna uyğun olaraq § 1 rationale-ı, § 2 trust anchor bəndləri, § 3 addım 4, § 4 rejected-alternatives izahı, Consequences və cross-reference-lər düzəldildi. `ADR-0039` bu ADR üçün authority deyil, yalnız historical context-dir.

Amendment scope-dan **kənar** qalanlar: signature placement (`ADR-0045`), manifest schema (`ADR-0036`), plugin trust UI/status contract (`ADR-0059`), license artifact lifecycle (`ADR-0060`).

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — `## Plugins və Licensing`
- `docs/02_ARCHITECTURE_MAP.md` — Row 27 `Plugin Packaging`, Row 38 `Plugin Trust Verification`, Cədvəl 3 `Plugin Signature Trust Model` risk sətri
- `docs/adr/ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md` — vendor private key custody və public anchor provisioning
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `signature` və `vendorId` field-ləri
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — canonical signed payload, files section və package fingerprint
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — key-purpose separation və trust status contract
- `docs/adr/ADR-0039-owner-password-recovery.md` — superseded history (`ADR-0058`); bu ADR üçün authority deyil
