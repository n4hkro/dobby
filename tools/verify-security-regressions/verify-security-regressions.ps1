[CmdletBinding()]
param(
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..')).Path
$common = @('-c', 'Release', '--logger', 'console;verbosity=minimal')
if ($NoBuild) {
    $common += '--no-build'
}

$unitFilter = 'FullyQualifiedName~Security|FullyQualifiedName~Licensing|FullyQualifiedName~Recovery|FullyQualifiedName~PluginFramework.Packaging|FullyQualifiedName~Throttle|FullyQualifiedName~Protected'
& dotnet test (Join-Path $repoRoot 'tests\Dobby.UnitTests\Dobby.UnitTests.csproj') @common --filter $unitFilter
if ($LASTEXITCODE -ne 0) {
    throw "Security unit regression suite failed with exit code $LASTEXITCODE."
}

$integrationFilter = 'FullyQualifiedName~Persistence|FullyQualifiedName~Plugins|FullyQualifiedName~Licensing|FullyQualifiedName~Recovery|FullyQualifiedName~Security'
& dotnet test (Join-Path $repoRoot 'tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj') @common --filter $integrationFilter
if ($LASTEXITCODE -ne 0) {
    throw "Security integration regression suite failed with exit code $LASTEXITCODE."
}
