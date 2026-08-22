# Portable Support Publish

Creates a clean, versioned, self-contained `win-x64` support/development
artifact from Release bits:

```powershell
.\tools\publish-portable\publish-portable.ps1 -NoRestore
```

The default output is:

```text
artifacts/publish/portable/<version>-<commit>/Dobby.Host/
```

`artifacts/publish/portable/latest.json` points to the latest exact output,
manifest and executable. `artifact-manifest.json` records source commit/tree
state, version, runtime, artifact mode and SHA-256/size for every payload file.

The script always removes and recreates its exact versioned target, fails if the
target cannot be cleaned, forces the Production trust profile, strips symbols,
runs customer-content checks and rewrites runtime metadata to:

- `Product.ArtifactMode = PortableSupport`
- `Runtime.DataRoot = %LocalAppData%/Dobby/PortableSupport` by default

Use `-SupportDataRoot` to supply a different isolated support root. Automated
launch tests must always override it with a fresh temporary directory through
`DOBBY__Runtime__DataRoot`; they must never use a real customer profile.

This artifact is not the primary customer deployment path and cannot satisfy the
MSIX install/upgrade or GA signing gates.
