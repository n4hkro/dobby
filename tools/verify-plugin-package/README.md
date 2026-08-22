# verify-plugin-package

Non-installing plugin-package verification wrapper. It reads and validates the
package, checks manifest/content rules and verifies the package signature without
loading or executing plugin code.

The wrapper invokes:

```powershell
Dobby.Admin.exe --cli verify-plugin-package
```

Required options:

- `--input-path`
- `--trust-profile` (`dev-qa` or `production`)
- `--host-version` (three-part version such as `1.0.0`)

Optional options:

- `--public-key-path` (explicit P-256 public-key PEM; otherwise the selected
  build profile's embedded key is used)
- `--report-path` (defaults to `<input-path>.verification.report.json`)
- `--overwrite`

Exit `0` means the exact input bytes were accepted. Exit `65` means the package,
profile, version, key or output contract was rejected. Reports contain only file
names, hashes, stage states and typed reason codes; they do not include package
content or full local paths.
