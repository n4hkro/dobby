# Sprint 11 Packaging Static Scan

Status: LOCAL STATIC SCAN PASSED; release readiness remains blocked by HD-S11-001 and HD-S11-003.

## Published Host Artifact

- Publish command: `dotnet publish .\src\Dobby.Host\Dobby.Host.csproj -c Release --no-restore`
- Host executable SHA-256: `0cbf66dc862b7e69ab5402789d546b2060b951bca6e6718260a06c2c525e1ce3`
- Host assembly SHA-256: `d29f6ff1e8801c9f808cb790ceb4db475b41314858fd8e6bcef78f01764c11a3`
- Security assembly SHA-256: `1c936aba16078b4150b98f71f58bd44ed01906e07512f290825a2d9a35db80a4`

## Scan Results

The publish output was scanned for sample plugin payloads, generated Dobby artifact extensions, dev/QA trust markers, and private-key-shaped files.

| Marker | File matches | Text matches |
| --- | ---: | ---: |
| `Dobby.Sample.Plugin` | 0 | 0 |
| `samples` | 0 | 0 |
| `.dobbyplugin` | 0 | 0 |
| `.dobbylicense` | 0 | 0 |
| `.dobbyrecovery` | 0 | 0 |
| `dev-qa` | 0 | 0 |
| `DevQa` | 0 | 0 |
| `PRIVATE KEY` | 0 | 0 |
| `.pfx` | 0 | n/a |
| `.snk` | 0 | n/a |
| `.key` | 0 | n/a |

## Conclusions

- No production output path includes sample plugin artifacts.
- dev/QA trust anchor absent from the scanned publish output.
- no production private keys were found in the scanned publish output.
- Generated `.dobbyplugin`, `.dobbylicense`, and `.dobbyrecovery` artifacts are absent from the scanned publish output.

## Blockers

HD-S11-001 still blocks production recovery handover, and HD-S11-003 still blocks installer/MSIX static scan evidence.
