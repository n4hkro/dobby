# ADR-0037: Plugin Compatibility and Lifecycle Versioning

**Status:** Accepted (revised 2026-08-22)  
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

The rules are fail-closed:

- an unknown manifest schema or package format is rejected;
- an unknown host API major is rejected;
- a plugin API minor greater than the host-supported minor is rejected;
- older minors within the supported major are compatible;
- plugin `version` and `minHostVersion` must be stable canonical SemVer values;
- a host below `minHostVersion` rejects the package before code is loaded.

Compatibility failures expose stable reason codes, not raw version-bearing or
path-bearing diagnostic strings. Installation and activation both call the same
matrix; activation does not reinterpret an installation decision.

## Lifecycle contract

Host API 2.0 introduces `IPluginModuleV2` with asynchronous initialize, activate,
deactivate and unload phases. Every phase receives a host-controlled cancellable
token and runs under a bounded timeout. Plugin-declared `Result` failures remain
expected outcomes; external cancellation, timeout and plugin exceptions remain
distinct typed failures.

The V1 adapter exists only for the 2.x migration window. Its use emits the
`legacy-v1-adapter-upgrade-required` warning and the
`plugin.lifecycle.v1-adapter-used` audit event. V1 support must be removed at the
next host API major gate; it must not be silently extended into host API 3.x.

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
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
