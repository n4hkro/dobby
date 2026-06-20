# sign-plugin-package

Dev/QA sample package signing wrapper.

The wrapper invokes:

```powershell
Dobby.Admin.exe --cli sign-plugin-package
```

Required options:

- `--input-path`
- `--signing-profile`
- `--output-path`

Optional options:

- `--trust-profile`
- `--report-path`
- `--overwrite`

The approved option and exit-code contract is documented in
`docs/specs/admin-tool.md`.
