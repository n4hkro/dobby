[CmdletBinding()]
param(
    [switch]$NoRestore,
    [string]$Version,
    [string]$OutputPath,
    [string]$SupportDataRoot = '%LocalAppData%/Dobby/PortableSupport'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $candidateFull = [System.IO.Path]::GetFullPath($Candidate)
    $rootPrefix = $rootFull.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidateFull.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside portable artifact root: $candidateFull"
    }
}

function Get-RelativeArtifactPath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    return [System.IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/')
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..')).Path
$projectPath = Join-Path $repoRoot 'src\Dobby.Host\Dobby.Host.csproj'
$portableRoot = Join-Path $repoRoot 'artifacts\publish\portable'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Dobby.Host project was not found at $projectPath."
}

$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the source commit for the portable manifest.'
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (& dotnet msbuild $projectPath -nologo -getProperty:Version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($Version)) {
        throw 'Unable to resolve the host version for the portable manifest.'
    }
}

if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]{0,63}$') {
    throw 'Version must be a bounded artifact-safe identifier.'
}

$artifactId = "$Version-$($commit.Substring(0, 12))"
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $portableRoot (Join-Path $artifactId 'Dobby.Host')
}

$outputFull = [System.IO.Path]::GetFullPath($OutputPath)
Assert-ChildPath -Root $portableRoot -Candidate $outputFull
if (Test-Path -LiteralPath $outputFull) {
    Remove-Item -LiteralPath $outputFull -Recurse -Force
}

if (Test-Path -LiteralPath $outputFull) {
    throw 'Portable output could not be cleaned; stale files may remain.'
}

New-Item -ItemType Directory -Force -Path $outputFull | Out-Null

$publishArgs = @(
    'publish',
    $projectPath,
    '-c',
    'Release',
    '-r',
    'win-x64',
    '--self-contained',
    'true',
    '-o',
    $outputFull,
    '/p:DobbyPluginTrustProfile=Production',
    '/p:PublishSingleFile=false',
    '/p:DebugType=none',
    '/p:DebugSymbols=false'
)

if ($NoRestore) {
    $publishArgs += '--no-restore'
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$requiredFiles = @(
    'Dobby.Host.exe',
    'Dobby.Host.dll',
    'appsettings.json',
    'native/win-x64/sqlite3mc_x64.dll'
)

$missingFiles = @(
    foreach ($relativePath in $requiredFiles) {
        $segments = $relativePath -split '/'
        $candidate = Join-Path $outputFull -ChildPath ([System.IO.Path]::Combine($segments))
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            $relativePath
        }
    }
)

if ($missingFiles.Count -gt 0) {
    throw "Portable publish output is missing required file(s): $($missingFiles -join ', ')"
}

$settingsPath = Join-Path $outputFull 'appsettings.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$settings.Dobby.Product.ArtifactMode = 'PortableSupport'
$settings.Dobby.Runtime.DataRoot = $SupportDataRoot
$settings | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $settingsPath -Encoding utf8NoBOM

$forbiddenFiles = @(
    Get-ChildItem -LiteralPath $outputFull -Recurse -File | Where-Object {
        $_.Extension -in @('.pdb', '.cs', '.map', '.dobbyplugin', '.dobbylicense') -or
        $_.Name -match '(?i)(test[-_.]?only|fixture|sample\.plugin|private[-_.]?key)'
    }
)
if ($forbiddenFiles.Count -gt 0) {
    throw "Portable customer-content scan rejected file: $($forbiddenFiles[0].Name)"
}

$textFiles = Get-ChildItem -LiteralPath $outputFull -Recurse -File | Where-Object {
    $_.Extension -in @('.json', '.config', '.xml', '.txt', '.md', '.ps1')
}
foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match '(?i)example\.(com|org)|placeholder|BEGIN PRIVATE KEY|dobby\.dev-qa|C:\\Users\\') {
        throw "Portable customer-content scan rejected text file: $($file.Name)"
    }
}

$statusLines = @(& git -C $repoRoot status --porcelain=v1 --untracked-files=no)
$files = @(
    Get-ChildItem -LiteralPath $outputFull -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = Get-RelativeArtifactPath -Root $outputFull -Path $_.FullName
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                size = $_.Length
            }
        }
)

$manifestPath = Join-Path $outputFull 'artifact-manifest.json'
$manifest = [ordered]@{
    schemaVersion = 1
    artifactMode = 'PortableSupport'
    artifactId = $artifactId
    sourceCommit = $commit
    sourceTreeDirty = $statusLines.Count -gt 0
    version = $Version
    runtimeIdentifier = 'win-x64'
    selfContained = $true
    supportDataRoot = $SupportDataRoot
    executable = 'Dobby.Host.exe'
    files = $files
}
$manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

$latestPath = Join-Path $portableRoot 'latest.json'
$latest = [ordered]@{
    artifactId = $artifactId
    outputPath = Get-RelativeArtifactPath -Root $portableRoot -Path $outputFull
    manifestPath = Get-RelativeArtifactPath -Root $portableRoot -Path $manifestPath
    executablePath = Get-RelativeArtifactPath -Root $portableRoot -Path (Join-Path $outputFull 'Dobby.Host.exe')
}
$latest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $latestPath -Encoding utf8NoBOM

$executablePath = Join-Path $outputFull 'Dobby.Host.exe'
$executableHash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Portable folder: $outputFull"
Write-Host "Manifest: $manifestPath"
Write-Host "Executable: $executablePath"
Write-Host "Executable SHA-256: $executableHash"
