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
