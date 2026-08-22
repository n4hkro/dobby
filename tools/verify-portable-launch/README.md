# Portable launch verification

Launches the exact versioned portable executable with a fresh, isolated
temporary data root, waits for an interactive login window, closes it normally,
requires a clean process exit and confirms that runtime data was written only to
the supplied root.

```powershell
.\tools\verify-portable-launch\verify-portable-launch.ps1 `
  -PortableRoot <versioned-Dobby.Host-folder> `
  -ReportPath <new-report.json>
```

`-MigrationFixturePath` explicitly copies a reviewed fixture into the isolated
root before launch. The tool never points at or cleans a customer profile.
