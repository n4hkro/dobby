[CmdletBinding()]
param(
    [switch]$NoRestore,
    [switch]$KeepExistingOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..')).Path
$projectPath = Join-Path $repoRoot 'src\Dobby.Host\Dobby.Host.csproj'
$outputPath = Join-Path $repoRoot 'artifacts\publish\portable\Dobby.Host'
$portableRoot = Join-Path $repoRoot 'artifacts\publish\portable'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Dobby.Host project was not found at $projectPath."
}

$portableRootFull = [System.IO.Path]::GetFullPath($portableRoot)
$outputFull = [System.IO.Path]::GetFullPath($outputPath)
$portableRootPrefix = $portableRootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $outputFull.StartsWith($portableRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean output outside portable artifact root: $outputFull"
}

if ((Test-Path -LiteralPath $outputFull) -and -not $KeepExistingOutput) {
    Remove-Item -LiteralPath $outputFull -Recurse -Force
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

Write-Host "Portable folder: $outputFull"
Write-Host "Executable: $(Join-Path $outputFull 'Dobby.Host.exe')"
