# Security regression verification

Runs the unit and integration slices that own artifact trust, package parsing,
licensing, recovery, authentication throttling, protected storage and persistence
fault boundaries.

```powershell
.\tools\verify-security-regressions\verify-security-regressions.ps1 -NoBuild
```
