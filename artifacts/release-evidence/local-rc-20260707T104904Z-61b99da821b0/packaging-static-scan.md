# Sprint 11 Packaging Static Scan

Status: PASS. Production publish output matches the Task 3 trust and recovery evidence requirements. Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

## Production Publish

- Publish command: `dotnet publish .\src\Dobby.Host\Dobby.Host.csproj -c Release --no-restore -p:DobbyPluginTrustProfile=Production`
- Published Host path: `src\Dobby.Host\bin\Release\net10.0-windows\publish\Dobby.Host.exe`
- Published security assembly path: `src\Dobby.Host\bin\Release\net10.0-windows\publish\Dobby.Security.dll`
- Host executable SHA-256: `61d64721d907d5ba7182303e4705fd4bd8f28018d0941a1cd670892192c54d3b`
- Host assembly SHA-256: `95fdf51854a888bea228cb4c339246fec05ef320ca40fa1e22607049ff973dc7`
- Security assembly SHA-256: `231bcb4555b6135b47b087efe2e4b09434a46af9bc35ab2d9fc4f074e58f33db`
Published security assembly embedded public-key SHA-256: `74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f`

## Scan Results

| Marker | Result |
| --- | --- |
| `Dobby.Sample.Plugin` | PASS (no matches) |
| `samples` | PASS (no matches) |
| `.dobbyplugin` | PASS (no matches) |
| `.dobbylicense` | PASS (no matches) |
| `.dobbyrecovery` | PASS (no matches) |
| `dev-qa` | PASS (no matches) |
| `DevQa` | PASS (no matches) |
| `PRIVATE KEY` | PASS (no matches) |
| `*.pfx` | PASS (0 files) |
| `*.snk` | PASS (0 files) |
| `*.key` | PASS (0 files) |

## Conclusions

- No production output path includes sample plugin artifacts.
- dev/QA trust anchor absent from the scanned publish output.
- no production private keys were found in the scanned publish output.
- Generated `.dobbyplugin`, `.dobbylicense`, and `.dobbyrecovery` artifacts are absent from the scanned publish output.
