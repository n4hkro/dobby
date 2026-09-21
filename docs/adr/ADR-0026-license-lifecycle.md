# ADR-0026: License Lifecycle

**Status:** Superseded by ADR-0051 (chain: ADR-0051 superseded by ADR-0060)
**Date:** 2026-04-24
**Superseded by:** ADR-0051, in turn superseded by ADR-0060

> Historical note: Code snippets and vocabulary below are not Phase 1 canonical.
> Current license lifecycle authority is `ADR-0060` (with the derived-state truth
> table in `docs/schemas/license-v3-lifecycle-truth-table.md`); plugin-facing
> runtime mode surface is `ADR-0053`; license device binding derivation is
> `ADR-0063`; signing key identity, purpose separation and trust anchors are
> `ADR-0062` (not the `ADR-0033` master-key model cited below); and the
> `ADR-0039` 5-hash composite device fingerprint referenced below is superseded
> history (`ADR-0058` — no fuzzy/quorum hardware matching, and license binding no
> longer shares a recovery fingerprint surface). Skeleton or documentation-fix work
> must not copy code/interface shapes from this ADR body without an explicit
> ADR-0060/ADR-0053 amendment.

## Context

`01_PRODUCT_SCOPE.md` `## Plugins və Licensing` Dobby core-un pulsuz və licensing-in plugin səviyyəsində tətbiq olunduğunu elan edir. Product scope plugin aktivasiyasının `müddətli lisenziya artefaktı` ilə idarə olunduğunu, artifact-ın müddət, expiry və `lazım gələrsə device/customer binding` daşıdığını və `allowed / denied` non-disclosing surface tələbini qoyur.

`02_ARCHITECTURE_MAP.md` Row 42 (`Dobby.Licensing`), Row 43 (`License / Entitlement`) və Cədvəl 3 `Dobby.Licensing` risk sətri Phase 1 host-gated model-i göstərir və `ADR-0026 Licensing model` ADR-ini tələb edir.

Engineering Convention `Licensing Surface Sərhədi` plugin-ə yalnız `allowed / denied` nəticə verilməsini qaydaya bağlayır.

Bu ADR license artifact format-ını, lifecycle-ı və plugin-visible surface-ni rəsmiləşdirir.

## Decision

Phase 1 license lifecycle modeli:

### 1. License artifact format

- License artifact vendor-signed JSON faylıdır (canonical JSON + ECDSA P-256 signature, `ADR-0032` ilə ortaq primitive).
- Payload field-ləri:
  - `licenseId` — unique GUID v7
  - `pluginId` — hansı plugin üçün
  - `status` — `allowed` və ya `denied`
  - `issuedAt` — ISO 8601 UTC
  - `expiresAt` — optional ISO 8601 UTC (expire olmazsa `null`)
  - `customerId` — optional, Phase 1-də opsionaldır (ADR-0035 tenant/customer context ilə uzlaşdırılır)
  - `deviceId` — optional device binding (ADR-0039 device fingerprint ilə ortaq surface)
  - `gracePeriodDays` — optional, default `7`
  - `keyVersion` — `1` Phase 1-də

### 2. License lifecycle state machine

```
[no-license] → (install artifact) → [active]
[active] → (expiresAt keçdi) → [grace]
[grace] → (gracePeriodDays keçdi) → [expired]
[expired] → (new artifact install) → [active]
[active|grace|expired] → (explicit revoke = new artifact with status: denied) → [denied]
```

### 3. Phase 1 enforcement (host-gated)

- Plugin activation zamanı `Dobby.Licensing / License / Entitlement` namespace-i plugin-in current license state-ini hesablayır.
- `active` və `grace` state-də plugin activation-a icazə verilir; `expired` və `denied` state-də plugin activation-a buraxılmır.
- `grace` state-də plugin activation-a icazə verilir amma audit warning yaranır (`license.grace.active`); user UI-da mesaj görür ("Lisenziya müddəti bitmişdir, X gün yenilənmə üçün vaxtınız var").
- `expired` state-də user UI-da plugin `Disabled (license expired)` kimi görünür.

### 4. Plugin-visible surface (non-disclosing)

Plugin yalnız bu interface-dən istifadə edə bilər:

```csharp
public interface ICapabilityLicensing {
    Result<bool> IsFeatureAllowed(string featureName);
}
```

Plugin **görə bilməz**:

- license id
- expiry date
- grace period status
- customer binding
- device binding
- raw license JSON

Bu `Engineering Convention / Licensing Surface Sərhədi` convention-ının enforcement-idir.

### 5. Customer binding (opsional)

- Customer binding Phase 1-də opsionaldır və hər deployment üçün məcburi deyil.
- Əgər license `customerId` daşıyırsa, host activation zamanı local deployment context-i ilə uyğunluğu yoxlayır; uyğunsuzluqda plugin `denied` hesab olunur.
- Customer binding-in runtime scope map-i ayrıca `tenant/customer` qərarı ilə dərinləşdirilə bilər; bu ADR-in core qərarı isə binding-in optional və host-controlled qalmasıdır.

### 6. Device binding (opsional)

- License artifact `deviceId` daşıyırsa, plugin activation zamanı device fingerprint (`ADR-0039` 5-hash composite) hesablanır və `deviceId` ilə müqayisə olunur.
- Uyğunsuzluqda plugin `denied` hesab olunur (license başqa cihaza köçürülüb).
- Device binding opsionaldır; default-da license device-independent olur.

### 7. Grace period

- `gracePeriodDays` default: `7`.
- Grace state vendor tərəfdən license yeniləmə üçün müştəriyə vaxt verir; bu müddətdə plugin işləyir, amma UI-da notification görünür.
- Grace period başa çatdıqdan sonra plugin `expired` state-ə keçir və activation-a buraxılmır.

### 8. Rejected alternatives

- **Online license server**: rədd edildi — offline-first prinsipi ilə uzlaşmır; license artifact-lar offline verify olunur.
- **Subscription lifecycle** (auto-renew): rədd edildi — Phase 1-də offline model; hər renewal yeni license artifact install tələb edir.
- **Plugin-visible expiry warning**: rədd edildi — UI warning host-un responsibility-sidir; plugin raw license detail görməməlidir.
- **Grace period plugin-configurable**: rədd edildi — grace period license artifact-da vendor tərəfindən təyin olunur, plugin manipulyasiya edə bilməz.

## Consequences

- Plugin author-lar licensing state-i öz-özlərinə yoxlamır; `ICapabilityLicensing.IsFeatureAllowed` çağırışı host-un yeganə API səthidir.
- Vendor license artifact-ı offline işləyən format-dır (JSON + ECDSA signature); müştəri internetsiz install edə bilər.
- Grace period müştəriyə vendor ilə yenilənmə danışıqları üçün operational buffer verir.
- License compromise aşkar olunarsa vendor yeni `denied` artifact issue edir və müştəriyə göndərir; install olunduqdan sonra plugin activation-a buraxılmır.
- Customer və device binding opsional olduğu üçün Phase 1 licensing modeli minimal və sadədir; enterprise multi-tenant bindings P2-də açılır.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — `## Plugins və Licensing`
- `docs/02_ARCHITECTURE_MAP.md` — Row 42 `Dobby.Licensing`, Row 43 `License / Entitlement`, Cədvəl 3 `Dobby.Licensing` risk sətri, Engineering Conventions `Licensing Surface Sərhədi`
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — ortaq ECDSA P-256 signature primitive
- `docs/adr/ADR-0033-signing-key-ownership.md` — vendor private key (license artifact signing)
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — `ICapabilityLicensing` surface
- `docs/adr/ADR-0039-owner-password-recovery.md` — ortaq device fingerprint
