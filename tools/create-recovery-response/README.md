# create-recovery-response

Owner recovery response generation wrapper.

The wrapper invokes:

```powershell
Dobby.Admin.exe --cli create-recovery-response
```

Required options:

- `--request-path`
- `--device-public-key-path`
- `--signing-profile`
- `--output-path`

Optional options:

- `--report-path`
- `--overwrite`

The approved option and exit-code contract is documented in
`docs/specs/admin-tool.md`.
