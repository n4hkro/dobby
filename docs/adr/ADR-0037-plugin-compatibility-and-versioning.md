# ADR-0037: Plugin Compatibility and Lifecycle Versioning

**Status:** Accepted (revised 2026-08-22; `hostApiVersion` contract-version rules and the § Lifecycle contract failure-classification/timeout surface amended by `ADR-0059` — see ## Revision History)
**Date:** 2026-04-24

## Context

Plugin compatibility must produce the same decision during package validation,
administrative tooling and runtime activation. Product version alone is not an
adequate proxy for the manifest, package format or SDK/host API contract.

## Decision

`PluginCompatibilityMatrix` is the single compatibility authority. The current
release supports this row:

| Manifest schema | Package format | Host API | Host product rule |
|---|---:|---|---|
| 2 | 2 | 2.0 | installed host version must be greater than or equal to `minHostVersion` |

The rules are fail-closed. `PluginCompatibilityMatrix` evaluates them in a fixed
order and returns the first matching stable reason code. There is no permissive
fallback for an absent, malformed or unrecognised value:

- an unknown manifest schema version is rejected (`unsupported-manifest-version`);
- an unknown package format version is rejected (`unsupported-package-format-version`);
- `hostApiVersion` is required, and an absent or non-`MAJOR.MINOR` value is rejected as
  malformed (`invalid-host-api-version`); it is never defaulted to the host's own
  version;
- a host API major other than the supported major is rejected
  (`unsupported-host-api-major`);
- a plugin API minor greater than the host-supported minor is rejected
  (`unsupported-host-api-minor`);
- older minors within the supported major are compatible;
- plugin `version` and `minHostVersion` must be stable canonical SemVer values: a
  `version` carrying a pre-release or build-metadata suffix is rejected as
  `invalid-manifest`, and a `minHostVersion` that is absent or not canonical SemVer is
  rejected as `invalid-minimum-host-version`;
- a host below `minHostVersion` rejects the package before code is loaded
  (`host-version-below-minimum`).

`minHostVersion` is a host product/build floor only. It does not stand in for the
`hostApiVersion` contract gate and cannot widen it: a plugin whose product floor the
installed host satisfies is still rejected when its API major differs from the
supported major or its API minor exceeds the host-supported minor. The two gates are
independent, and both are evaluated.

Compatibility failures expose stable reason codes, not raw version-bearing or
path-bearing diagnostic strings. Installation and activation both call the same
matrix; activation does not reinterpret an installation decision. A plugin that fails
the matrix at activation is `Rejected`, not `Skipped`; `Skipped` is reserved for a
plugin the host never evaluated, including one whose dependency was rejected
(`ADR-0009`).

## Lifecycle contract

Host API 2.0 introduces `IPluginModuleV2` with asynchronous initialize, activate,
deactivate and unload phases. Every phase receives a host-controlled cancellable
token and runs under a bounded timeout. Timeouts are host-owned and
validated-bounded: a plugin can neither read nor extend them. One operation per
plugin instance is active at a time, and the host performs deterministic cleanup
after failure, timeout or cancellation so no half-registered plugin state survives.

Plugin-declared `Result` failures remain expected outcomes and are not faults.
External cancellation, timeout, plugin exception, contract breach,
authorization/license denial and host infrastructure failure remain distinct retained
outcome origins. The complete outcome-origin set, the activation status each origin
maps to, and the bounded process-local quarantine rule are owned by `ADR-0008` § 4 and
`ADR-0059`; only the four defect origins (timeout, plugin-declared failure, plugin
exception, contract breach) count toward quarantine, so an expected rejection can
never accumulate into one.

The V1 adapter exists only for the 2.x migration window. Its use emits the
`legacy-v1-adapter-upgrade-required` warning and the
`plugin.lifecycle.v1-adapter-used` audit event. That warning is the only warning an
`Active` activation outcome carries in host API 2.x, and the audit mapping keys the
v1-adapter event on the presence of a warning on an `Active` outcome — so introducing a
second `Active`-outcome warning value requires the audit mapping and
`docs/specs/audit-and-diagnostics.md` to be re-keyed in the same change, otherwise the
new warning is recorded as a v1 adapter use that never happened.

V1 support must be removed at the next host API major gate; it must not be silently
extended into host API 3.x. Removal is a host API 3.0 gate item: the compatibility
matrix raises its supported major, the adapter dispatch path and its warning are
deleted rather than left unreachable, and the audit event name stays in the closed
registry as historical vocabulary so existing audit records remain readable.

Plugin assembly load contexts remain process-lifetime and non-collectible under
ADR-0007. Logical deactivate/unload removes registrations, subscriptions, work
and disposable instances. Updating or uninstalling a plugin activated in the
current process is marked restart-required because physical assembly unload is
not claimed.

## Consequences

- Tools and runtime share one table-driven decision.
- No unknown API major is allowed to execute.
- Cleanup is attempted with a bounded host token even when the initiating user
  operation is cancelled.
- Reliable collectible unload remains deferred until isolation tests prove that
  no plugin references, handlers, threads or registrations survive.

## Cross-references

- `docs/adr/ADR-0007-plugin-load-context-strategy.md`
- `docs/adr/ADR-0008-deferred-plugin-activation.md` — § 2 activation-time gate order and
  § 4 outcome-origin/quarantine owner
- `docs/adr/ADR-0009-plugin-dependency-graph.md` — `Skipped` semantics for a plugin whose
  dependency was rejected
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
- `docs/specs/audit-and-diagnostics.md` — closed audit event registry, including
  `plugin.lifecycle.v1-adapter-used`
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — current authority for the
  `hostApiVersion` contract-version gate and the lifecycle failure-classification/timeout
  surface amended out of this ADR

## Revision History

- **2026-08-31:** Amendment record yazıldı və relation vocabulary düzəldildi.
  `**Status:**` sətri əvvəl `hostApiVersion` və lifecycle portion-larının `ADR-0059` ilə
  "superseded" olduğunu bildirirdi; `docs/adr/README.md` bu sözü yalnız tam supersession
  üçün ayırır (orada adlanan ADR-in `Status`-u `Superseded` olmalıdır) və README-nin öz
  Supersession Index-i bu ADR-i `Accepted (partial)` kimi göstərir. Bu sənəd tam retire
  olunmayıb: `minHostVersion` product/build floor semantikası, plugin-facing breaking
  change idarəsi və qalan compatibility qaydaları burada qalır. Əlavə olaraq bu ADR-də nə
  dated bölmə var idi, nə də `ADR-0059` `## Cross-references`-də adlanırdı -- README-nin
  birgə tələb etdiyi iki şərtdən heç biri ödənilmirdi və `ADR-0059` bu ADR-i öz
  `**Amends:**` header-ində claim etmirdi. Üçü də bu tarixdə bağlandı.
- **2026-08-31:** § Decision compatibility rules və § Lifecycle contract `ADR-0059`,
  `ADR-0008` § 2/§ 4 və shipped `PluginCompatibilityMatrix` ilə tam uzlaşdırıldı.
  (a) Fail-closed siyahısı yalnız "unknown" dəyərləri sayırdı; absent və ya malformed
  `hostApiVersion`, pre-release/build-metadata `version` və qeyri-canonical
  `minHostVersion` üçün heç bir sətir yox idi. Bir reader "unknown deyilsə keç" məntiqi
  ilə permissive fallback yazıb malformed manifest-i qəbul edə bilərdi; siyahı artıq
  fixed evaluation order-i və yeddi stable reason code-un hamısını adlandırır.
  (b) `minHostVersion`-un yalnız product/build floor olduğu və `hostApiVersion`
  contract gate-ini əvəz etmədiyi explicit yazıldı — cədvəldəki "installed host version
  must be ≥ `minHostVersion`" sətri tək oxunanda iki gate-in birləşdirilməsinə imkan
  verirdi.
  (c) Matrix failure-in `Rejected` (yox `Skipped`) olduğu və `Skipped`-in
  dependency-rejected plugin üçün ayrıldığı yazıldı (`ADR-0009`, `AC-049`).
  (d) § Lifecycle contract yalnız üç failure kind sadalayırdı ("cancellation, timeout,
  plugin exceptions"); contract breach, authorization/license denial və host
  infrastructure failure görünmürdü, ona görə reader taxonomy-nin tam olduğunu fərz edib
  expected rejection-ı defect kimi say edə bilərdi. Origin set-in owner-i (`ADR-0008` § 4,
  `ADR-0059`) və quarantine-ə yalnız dörd defect origin-in sayıldığı əlavə edildi.
  Host-owned validated-bounded timeout, instance başına bir aktiv əməliyyat və
  deterministik cleanup da bu bölmədə explicit oldu.
  (e) `plugin.lifecycle.v1-adapter-used` audit mapping-inin shipped şəkildə
  `Active` + warning mövcudluğuna key olunduğu yazıldı; ikinci bir `Active`-outcome
  warning dəyəri əlavə etmək mapping-i eyni change-də re-key etməyi tələb edir, əks halda
  yeni warning heç vaxt baş vermədiyi v1 adapter istifadəsi kimi audit olunur.
  (f) V1 removal gate-i konkretləşdirildi (supported major artımı, adapter path-ın
  silinməsi, audit adının closed registry-də historical vocabulary kimi qalması) — əvvəlki
  "must be removed" cümləsi heç bir artefakt adlandırmırdı, yəni yerinə yetirilib
  yetirilmədiyi yoxlanıla bilməzdi. Qərar dəyişmir; bu, mövcud qərarın verifiable
  ifadəsidir.
