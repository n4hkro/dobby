[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repoRoot 'tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj'
$arguments = @(
    'test',
    $project,
    '-c',
    $Configuration,
    '--filter',
    'FullyQualifiedName~TraceabilityRegisterTests'
)

if ($NoRestore) {
    $arguments += '--no-restore'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Traceability verification failed with exit code $LASTEXITCODE."
}
