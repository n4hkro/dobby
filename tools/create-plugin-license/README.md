# create-plugin-license

Detached sample `.dobbylicense` creation wrapper.

The wrapper invokes:

```powershell
Dobby.Admin.exe --cli create-plugin-license
```

Required options:

- `--plugin-package-path`
- `--signing-profile`
- `--customer-id`
- `--deployment-id`
- `--device-binding-fixture-path`
- `--valid-from`
- `--valid-until`
- `--revision`
- `--output-path`

Optional options:

- `--report-path`
- `--overwrite`

The approved option and exit-code contract is documented in
`docs/specs/admin-tool.md`.
