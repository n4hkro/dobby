[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..')).Path
$outputFull = [System.IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $outputFull) -and -not $Overwrite) {
    $existing = @(Get-ChildItem -LiteralPath $outputFull -Force)
    if ($existing.Count -gt 0) {
        throw 'SBOM output directory must be empty unless -Overwrite is supplied.'
    }
}

New-Item -ItemType Directory -Force -Path $outputFull | Out-Null
$projectFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.csproj' -Recurse -File
    Get-ChildItem -LiteralPath (Join-Path $repoRoot 'samples') -Filter '*.csproj' -Recurse -File
) | Sort-Object FullName

$packageIndex = [ordered]@{}
$lockFiles = @()
foreach ($projectFile in $projectFiles) {
    $assetsPath = Join-Path $projectFile.DirectoryName 'obj\project.assets.json'
    if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
        throw "Restore assets are missing for project $($projectFile.Name)."
    }

    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($libraryName in @($assets.libraries.Keys | Sort-Object)) {
        $library = $assets.libraries[$libraryName]
        if ($library.type -ne 'package') {
            continue
        }

        $separator = $libraryName.LastIndexOf('/')
        if ($separator -le 0) {
            throw "Invalid package lock entry '$libraryName'."
        }

        $name = $libraryName.Substring(0, $separator)
        $version = $libraryName.Substring($separator + 1)
        $key = "$($name.ToLowerInvariant())/$version"
        if (-not $packageIndex.Contains($key)) {
            $packageIndex[$key] = [ordered]@{
                type = 'library'
                name = $name
                version = $version
                purl = "pkg:nuget/$name@$version"
                scope = 'required'
            }
        }
    }

    $lockFiles += [ordered]@{
        project = [System.IO.Path]::GetRelativePath($repoRoot, $projectFile.FullName).Replace('\', '/')
        assets = [System.IO.Path]::GetRelativePath($repoRoot, $assetsPath).Replace('\', '/')
        assetsSha256 = (Get-FileHash -LiteralPath $assetsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to resolve source commit.'
}

$hostProject = Join-Path $repoRoot 'src\Dobby.Host\Dobby.Host.csproj'
$version = (& dotnet msbuild $hostProject -nologo -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to resolve product version.'
}

$sbom = [ordered]@{
    bomFormat = 'CycloneDX'
    specVersion = '1.6'
    version = 1
    metadata = [ordered]@{
        component = [ordered]@{
            type = 'application'
            name = 'Dobby'
            version = $version
            'bom-ref' = "pkg:generic/dobby@${version}?commit=$commit"
        }
        properties = @(
            [ordered]@{ name = 'dobby:sourceCommit'; value = $commit }
            [ordered]@{ name = 'dobby:restoreProvenance'; value = 'project.assets.json' }
        )
    }
    components = @($packageIndex.Values | Sort-Object name, version)
}

$inventory = [ordered]@{
    schemaVersion = 1
    sourceCommit = $commit
    productVersion = $version
    projects = $lockFiles
    packages = @($packageIndex.Values | Sort-Object name, version)
    licenseReview = [ordered]@{
        status = 'owner-review-required'
        note = 'Package license identifiers require release-owner review and acceptance before GA.'
    }
}

$sbomPath = Join-Path $outputFull 'sbom.cdx.json'
$inventoryPath = Join-Path $outputFull 'dependency-inventory.json'
$sbom | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $sbomPath -Encoding utf8NoBOM
$inventory | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $inventoryPath -Encoding utf8NoBOM

$vulnerabilityPath = Join-Path $outputFull 'vulnerability-scan.json'
$vulnerabilityOutput = & dotnet list $hostProject package --vulnerable --include-transitive --format json --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    $vulnerabilityOutput | Set-Content -LiteralPath (Join-Path $outputFull 'vulnerability-scan.failed.log') -Encoding utf8NoBOM
    throw "NuGet vulnerability scan failed with exit code $LASTEXITCODE."
}

$vulnerabilityOutput | Set-Content -LiteralPath $vulnerabilityPath -Encoding utf8NoBOM
$parsedVulnerability = Get-Content -LiteralPath $vulnerabilityPath -Raw | ConvertFrom-Json
$vulnerablePackages = @(
    foreach ($project in @($parsedVulnerability.projects)) {
        if ($project.PSObject.Properties.Name -notcontains 'frameworks') {
            continue
        }

        foreach ($framework in @($project.frameworks)) {
            if ($framework.PSObject.Properties.Name -contains 'topLevelPackages') {
                @($framework.topLevelPackages) | Where-Object {
                    $_.PSObject.Properties.Name -contains 'vulnerabilities'
                }
            }

            if ($framework.PSObject.Properties.Name -contains 'transitivePackages') {
                @($framework.transitivePackages) | Where-Object {
                    $_.PSObject.Properties.Name -contains 'vulnerabilities'
                }
            }
        }
    }
)
if ($vulnerablePackages.Count -gt 0) {
    throw "NuGet vulnerability scan found $($vulnerablePackages.Count) vulnerable package entries."
}

Write-Host "SBOM: $sbomPath"
Write-Host "Dependency inventory: $inventoryPath"
Write-Host "Vulnerability scan: $vulnerabilityPath"
