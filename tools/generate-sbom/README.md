# Generate SBOM and dependency evidence

```powershell
.\tools\generate-sbom\generate-sbom.ps1 -OutputDirectory <new-empty-directory>
```

The script generates a CycloneDX 1.6 SBOM, a project/assets-file dependency
inventory and a NuGet vulnerability result for the host dependency closure. It
fails when restore assets are missing, the output is stale, the audit command
fails or a vulnerable package is reported.

Package license review remains an explicit release-owner gate. The generated
inventory marks it `owner-review-required`; automation must not silently convert
missing legal approval into a passing GA claim.
