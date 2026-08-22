# ADR-0059: Plugin Trust, Package, and UI Contract v2

**Status:** Accepted

**Date:** 2026-08-21

## Context

The host loads signed vendor assemblies in-process through
`AssemblyLoadContext`. Existing documents call this a sandbox and claim that
reflection, filesystem, shared-static, and other backdoors are blocked. An
assembly load context provides dependency isolation, not a security boundary.

Package reading currently materializes every ZIP entry before bounded
validation, and staging relies on string/path checks without a final resolved
destination containment check. The public page model supports only text and
buttons, renders localization keys as literal UI text, and executes actions with
`CancellationToken.None`. The synchronous activation hook can block without a
timeout. Compatibility has `minHostVersion` but no explicit public host API
major version.

## Decision

### Trust posture

- In-process plugins are fully trusted code signed by an approved vendor key.
  `AssemblyLoadContext`, assembly-reference scans, and contract conventions are
  compatibility and hygiene mechanisms; they are not called a sandbox.
- A package that is not fully trusted must not execute in the host process.
  Supporting untrusted or semi-trusted vendors requires a separately approved
  out-of-process worker with OS-enforced identity, IPC, resource, and file/network
  boundaries.
- Static forbidden-content scans remain defense in depth and must not be used as
  proof that arbitrary .NET APIs are contained.

### Package v2 safety

- Package parsing is streaming and enforces configured hard limits before
  allocation/extraction: compressed bytes, manifest bytes, entry count,
  per-entry uncompressed bytes, aggregate uncompressed bytes, path length, and
  compression ratio.
- Every entry name is normalized once. Empty segments, absolute/drive/UNC paths,
  alternate separators, trailing dot/space aliases, ADS syntax, Windows device
  names including extension variants, symlinks/reparse points, duplicates, and
  case collisions are rejected.
- Extraction computes `Path.GetFullPath` for the destination, verifies containment
  under a separator-terminated staging root, refuses overwrite, and performs
  no-follow checks at write time. Validation and materialization use the same
  canonical path object.
- Limits are part of the signed manifest profile and host release configuration;
  tests include flat ZIP bombs, integer overflow, and path alias cases.

### Signatures, fingerprint, and keys

- Plugin package signatures use ECDSA P-256/SHA-256 with the signature format
  explicitly set to `IeeeP1363FixedFieldConcatenation` and encoded base64url
  without padding. Verification specifies the same format; provider defaults are
  not relied upon.
- The signed bytes are domain-separated and versioned. The package fingerprint is
  `SHA-256(domain || canonical-manifest-without-signature)`; the manifest's
  canonical `files` map is included exactly once.
- Plugin-package, license, and recovery signing are separate purposes and use
  distinct versioned leaf keys/key ids. A vendor offline root may authorize leaf
  keys, but one interchangeable private key is forbidden.

### Compatibility and lifecycle

- The manifest adds `hostApiVersion` as a required `MAJOR.MINOR` contract version.
  Major mismatch rejects activation. Minor compatibility follows an explicit
  backward-compatibility matrix. `minHostVersion` remains a product/build floor,
  not a substitute for API compatibility.
- Module registration and activation become asynchronous/cancellable at the host
  boundary. Activation, deactivation, page lifecycle, and action execution have
  bounded timeouts, one active operation per instance, and deterministic cleanup.
- Expected results such as `Forbidden`, `InvalidInput`, `LicenseReadOnly`, and
  cancellation are not recorded as plugin faults. Only unexpected failures and
  lifecycle contract violations produce fault diagnostics/status.
- Runtime calls retain an explicit outcome origin after safe error mapping:
  expected rejection, authorization/license denial, cancellation, timeout,
  plugin-declared failure, thrown plugin exception, contract breach, or host
  infrastructure failure. Returned validation errors therefore cannot be
  confused with plugin code that threw an argument exception.
- The process-local quarantine policy counts only timeout, plugin-declared
  failure, thrown exception, and contract-breach outcomes. A completed operation
  resets the defect streak; expected and host failures are neutral. Reaching the
  bounded threshold removes the plugin's routes, event subscriptions, services,
  operations, and work sessions through the normal lifecycle cleanup boundary.

### Declarative host-rendered UI v2

- Third-party UI uses a versioned declarative component tree owned by
  `Dobby.Contracts`: localized text, form fields, validation, commands, tables,
  lists, status/progress, and layout primitives. Plugins do not return WPF types
  or delegates embedded directly in view models.
- Actions are identified by ids and dispatched through an async host command
  boundary with cancellation, authorization, license mode, timeout, correlation,
  and result mapping.
- Plugin localization is resolved through a plugin resource provider keyed by
  plugin id/culture/key. Resource keys are never rendered as user-facing text.
- Schema/version negotiation rejects unsupported components before page
  activation and provides a safe localized fallback.

## Consequences

- The delegate-bearing text/action page contract is removed at the v2 UI gate;
  the lifecycle v1 adapter remains only for the separately documented module
  migration window.
- The sample plugin must prove the v2 component, localization, cancellation,
  progress, failure, and work-session paths from a real signed package.
- The host cannot claim hostile-code containment while using in-process plugins.
- This ADR supersedes the security-boundary claims in ADR-0011/ADR-0012, clarifies
  ADR-0007, and supersedes the minimum page/lifecycle portions of ADR-0006,
  ADR-0021, ADR-0036, ADR-0037, and ADR-0049 where they conflict.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Plugin Framework, Contracts, SDK, Shell/UI rows
- `docs/03_TECH_STACK_AND_VERSIONS.md` — plugin packaging and cryptography
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-007, FR-008, FR-026
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — NFR-004, NFR-006, NFR-008, NFR-009
- `docs/specs/plugins-manager.md`
- `docs/specs/sample-plugin.md`
- Microsoft plugin guidance: https://learn.microsoft.com/dotnet/core/tutorials/creating-app-with-plugin-support
- Microsoft archive safety guidance: https://learn.microsoft.com/dotnet/standard/io/zip-tar-best-practices
- .NET signature formats: https://learn.microsoft.com/dotnet/api/system.security.cryptography.dsasignatureformat
