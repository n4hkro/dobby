[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,
    [switch]$NoRestore,
    [switch]$SourceOnly,
    [string]$InstalledMsixEvidencePath,
    [string]$ProductionSigningEvidencePath,
    [string]$LicenseReviewEvidencePath,
    [string]$SamplePluginPackagePath,
    [string]$SamplePluginPublicKeyPath,
    [ValidateSet('dev-qa', 'production')]
    [string]$SamplePluginTrustProfile = 'dev-qa'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..')).Path
$evidenceFull = [System.IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $evidenceFull) {
    if (@(Get-ChildItem -LiteralPath $evidenceFull -Force).Count -gt 0) {
        throw 'EvidenceRoot must be new or empty; stale evidence is forbidden.'
    }
}
else {
    New-Item -ItemType Directory -Path $evidenceFull | Out-Null
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to resolve source commit.'
}

$status = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=no)
if ($status.Count -gt 0) {
    throw 'Release evidence requires a clean tracked source tree.'
}

$results = [System.Collections.Generic.List[object]]::new()
$blockers = [System.Collections.Generic.List[object]]::new()

function Invoke-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    $logPath = Join-Path $evidenceFull "$Name.log"
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $exitCode = 0
    try {
        & $Action *>&1 | Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            throw "Gate '$Name' failed with exit code $exitCode."
        }
    }
    catch {
        if ($exitCode -eq 0) {
            $exitCode = 1
        }

        $_ | Out-String | Add-Content -LiteralPath $logPath -Encoding utf8NoBOM
        throw
    }
    finally {
        $stopwatch.Stop()
        $results.Add([ordered]@{
            name = $Name
            command = $Command
            exitCode = $exitCode
            durationMilliseconds = $stopwatch.ElapsedMilliseconds
            log = [System.IO.Path]::GetRelativePath($evidenceFull, $logPath).Replace('\', '/')
        })
    }
}

function Add-ExternalEvidence {
    param(
        [Parameter(Mandatory = $true)][string]$Gate,
        [string]$Path,
        [Parameter(Mandatory = $true)][string]$OwnerInput
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $blockers.Add([ordered]@{ gate = $Gate; ownerInput = $OwnerInput })
        return
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $results.Add([ordered]@{
        name = $Gate
        command = 'external-evidence-validation'
        exitCode = 0
        durationMilliseconds = 0
        evidenceFileName = [System.IO.Path]::GetFileName($resolved)
        evidenceSha256 = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash.ToLowerInvariant()
    })
}

Invoke-Gate -Name '00-dotnet-info' -Command 'dotnet --info' -Action { & dotnet --info }
if (-not $NoRestore) {
    Invoke-Gate -Name '01-restore' -Command 'dotnet restore Dobby.sln' -Action {
        & dotnet restore (Join-Path $repoRoot 'Dobby.sln')
    }
}

Invoke-Gate -Name '02-format' -Command 'dotnet format Dobby.sln --verify-no-changes --no-restore' -Action {
    & dotnet format (Join-Path $repoRoot 'Dobby.sln') --verify-no-changes --no-restore
}
Invoke-Gate -Name '03-build' -Command 'dotnet build Dobby.sln -c Release --no-restore' -Action {
    & dotnet build (Join-Path $repoRoot 'Dobby.sln') -c Release --no-restore
}
Invoke-Gate -Name '04-unit' -Command 'dotnet test Dobby.UnitTests -c Release --no-build' -Action {
    & dotnet test (Join-Path $repoRoot 'tests\Dobby.UnitTests\Dobby.UnitTests.csproj') -c Release --no-build --logger 'console;verbosity=minimal'
}
Invoke-Gate -Name '05-integration' -Command 'dotnet test Dobby.IntegrationTests -c Release --no-build' -Action {
    & dotnet test (Join-Path $repoRoot 'tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj') -c Release --no-build --logger 'console;verbosity=minimal'
}
Invoke-Gate -Name '06-smoke' -Command 'dotnet test Dobby.SmokeTests -c Release --no-build' -Action {
    & dotnet test (Join-Path $repoRoot 'tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj') -c Release --no-build --logger 'console;verbosity=minimal'
}
Invoke-Gate -Name '07-flake-sensitive' -Command 'verify-flake-sensitive-tests.ps1 -NoBuild' -Action {
    & (Join-Path $repoRoot 'tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1') -NoBuild
}
Invoke-Gate -Name '08-security-regressions' -Command 'verify-security-regressions.ps1 -NoBuild' -Action {
    & (Join-Path $repoRoot 'tools\verify-security-regressions\verify-security-regressions.ps1') -NoBuild
}
Invoke-Gate -Name '09-sbom' -Command 'generate-sbom.ps1' -Action {
    & (Join-Path $repoRoot 'tools\generate-sbom\generate-sbom.ps1') -OutputDirectory (Join-Path $evidenceFull 'supply-chain')
}
Invoke-Gate -Name '10-portable' -Command 'publish-portable.ps1 -NoRestore' -Action {
    & (Join-Path $repoRoot 'tools\publish-portable\publish-portable.ps1') -NoRestore
}
Invoke-Gate -Name '11-portable-launch' -Command 'verify-portable-launch.ps1 (isolated fresh root)' -Action {
    $latestPath = Join-Path $repoRoot 'artifacts\publish\portable\latest.json'
    $latest = Get-Content -LiteralPath $latestPath -Raw | ConvertFrom-Json
    $portableRoot = Join-Path (Join-Path $repoRoot 'artifacts\publish\portable') ([string]$latest.outputPath).Replace('/', '\')
    & (Join-Path $repoRoot 'tools\verify-portable-launch\verify-portable-launch.ps1') `
        -PortableRoot $portableRoot `
        -ReportPath (Join-Path $evidenceFull 'portable-launch.json')
}

if ($SamplePluginPackagePath -and $SamplePluginPublicKeyPath) {
    $pluginReport = Join-Path $evidenceFull 'sample-plugin-verification.json'
    Invoke-Gate -Name '12-sample-plugin' -Command 'verify-plugin-package (redacted arguments)' -Action {
        & (Join-Path $repoRoot 'tools\verify-plugin-package\verify-plugin-package.ps1') `
            --input-path $SamplePluginPackagePath `
            --trust-profile $SamplePluginTrustProfile `
            --host-version 1.0.0 `
            --public-key-path $SamplePluginPublicKeyPath `
            --report-path $pluginReport
    }
}
else {
    $blockers.Add([ordered]@{
        gate = 'sample-plugin-artifact'
        ownerInput = 'Exact signed sample package and matching public verification key'
    })
}

Add-ExternalEvidence -Gate 'installed-msix-upgrade' -Path $InstalledMsixEvidencePath -OwnerInput 'Disposable-account/VM N-to-N+1 installed upgrade evidence'
Add-ExternalEvidence -Gate 'production-signing' -Path $ProductionSigningEvidencePath -OwnerInput 'Production certificate, publisher, timestamp and artifact-signature evidence'
Add-ExternalEvidence -Gate 'license-review' -Path $LicenseReviewEvidencePath -OwnerInput 'Release-owner dependency license review/acceptance'

$portableLatestPath = Join-Path $repoRoot 'artifacts\publish\portable\latest.json'
$portableLatest = Get-Content -LiteralPath $portableLatestPath -Raw | ConvertFrom-Json
$manifest = [ordered]@{
    schemaVersion = 1
    scope = if ($SourceOnly) { 'source-only' } else { 'release-candidate' }
    sourceCommit = $commit
    sourceTreeCleanAtStart = $true
    dotnetSdk = (& dotnet --version).Trim()
    results = $results
    portable = $portableLatest
    externalBlockers = $blockers
    releaseReady = $blockers.Count -eq 0
}

$manifestPath = Join-Path $evidenceFull 'manifest.json'
$manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
$summaryPath = Join-Path $evidenceFull 'summary.md'
$summaryLines = @(
    '# Release candidate verification'
    ''
    "- Source commit: ``$commit``"
    "- Scope: ``$($manifest.scope)``"
    "- Automated gates passed: $($results.Count)"
    "- External blockers: $($blockers.Count)"
    "- Release ready: $($manifest.releaseReady)"
)
foreach ($blocker in $blockers) {
    $summaryLines += "- BLOCKED ``$($blocker.gate)``: $($blocker.ownerInput)"
}
$summaryLines | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM

if (-not $SourceOnly -and $blockers.Count -gt 0) {
    throw "Release candidate is blocked by $($blockers.Count) missing external evidence inputs."
}

Write-Host "Evidence manifest: $manifestPath"
Write-Host "Evidence summary: $summaryPath"
