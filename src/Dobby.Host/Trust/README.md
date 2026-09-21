# Dobby Trust Resources

Production and dev/QA public trust keys are approved build inputs. Package,
license, and Owner-recovery signatures use purpose-separated keys and key IDs;
their public keys are embedded only by the selected build profile. Only public
PEM files belong in this folder. Private keys must stay outside the workspace,
repository, process arguments, logs, and reports.

Accepted plugins are reviewed, signed, fully trusted in-process product code.
Signature verification, compatibility preflight, capability wrappers and
`AssemblyLoadContext` do not sandbox hostile code. A package that is not fully
trusted must never be loaded into the host process.

Legacy shared `vendor-master` and `dev-qa` trust anchors are not supported.
Rotation is performed through bounded, versioned verification rings with
not-before and retirement metadata. Production builds fail closed if a dev/QA
or TEST-ONLY key is present in the selected trust ring.

## Anchor provenance (pre-GA)

The three committed `vendor-*-public-key.pem` anchors are locally generated
GA-candidate placeholders. They are real P-256 public keys with real private
counterparts held outside this repository, so every build, publish and
verification path exercises the production code path correctly, but they were
not issued or handed over by an authorized release custodian.

Consequences, per `HD-S11-001` in
`docs/superpowers/plans/2026-07-07-p1-release-gate-closure.md`:

- The anchors must be replaced with the approved production public PEMs before
  GA. Replacement is a public-PEM-only change to these files plus the pinned
  hashes in `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`.
- A custody hash that this repository produced matches by construction and is
  not custody evidence. `HD-S11-001` closes only when the release authority
  supplies the approved public SHA-256 out of band, independently of this
  repository, and it equals the committed anchor.
- Artifacts signed with the current placeholder counterparts are RC-mechanics
  evidence only. They must not be presented as customer-trustable production
  artifacts.

The `dev-qa-*` anchors are intentionally non-production and are never embedded
by the Production build profile.
