# Portable Publish

Creates a local support/dev portable folder for manual testing.

```powershell
.\tools\publish-portable\publish-portable.ps1
```

Default output:

```text
artifacts/publish/portable/Dobby.Host/
```

Launch the app from:

```text
artifacts/publish/portable/Dobby.Host/Dobby.Host.exe
```

This flow is not the primary customer deployment path. MSIX remains the primary
deployment format; this folder is for support, development, and offline test use.
