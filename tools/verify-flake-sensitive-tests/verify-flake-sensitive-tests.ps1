[CmdletBinding()]
param(
    [ValidateSet('Local', 'Release')]
    [string]$Profile = 'Local',

    [int]$FocusedIterations = -1,

    [int]$IntegrationIterations = -1,

    [int]$SolutionIterations = -1,

    [switch]$NoBuild,

    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot '..\..')
Set-Location $repoRoot

if ($FocusedIterations -lt -1 -or $IntegrationIterations -lt -1 -or $SolutionIterations -lt -1) {
    throw 'Iteration counts must be -1 for profile defaults or zero/positive integers.'
}

if ($FocusedIterations -lt 0) {
    $FocusedIterations = if ($Profile -eq 'Release') { 20 } else { 5 }
}

if ($IntegrationIterations -lt 0) {
    $IntegrationIterations = if ($Profile -eq 'Release') { 10 } else { 2 }
}

if ($SolutionIterations -lt 0) {
    $SolutionIterations = if ($Profile -eq 'Release') { 5 } else { 1 }
}

$totalTestIterations = $FocusedIterations + $IntegrationIterations + $SolutionIterations
if ($totalTestIterations -le 0) {
    throw 'At least one test iteration is required for flake-sensitive verification.'
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logRoot = Join-Path $repoRoot "TestResults\flake-sensitive\$timestamp"
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

$summaryPath = Join-Path $logRoot 'summary.md'
$startedAt = Get-Date

function Format-CommandLine {
    param([string[]]$Arguments)

    return 'dotnet ' + (($Arguments | ForEach-Object {
        if ($_ -match '\s') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }) -join ' ')
}

function Escape-MarkdownCell {
    param([string]$Value)

    return ($Value -replace '\|', '\|') -replace "`r?`n", ' '
}

function Add-SummaryLine {
    param([string]$Line)

    Add-Content -Path $summaryPath -Value $Line -Encoding UTF8
}

@(
    '# Flake-Sensitive Verification Summary'
    ''
    "- Profile: $Profile"
    "- Repository root: $repoRoot"
    "- Configuration: $Configuration"
    "- Build first: $(-not $NoBuild)"
    "- Focused iterations: $FocusedIterations"
    "- Integration iterations: $IntegrationIterations"
    "- Solution iterations: $SolutionIterations"
    "- Started: $($startedAt.ToString('O'))"
    ''
    '| Phase | Iteration | Command | Start | End | Duration | Exit code | Stdout | Stderr | Result |'
    '| --- | ---: | --- | --- | --- | ---: | ---: | --- | --- | --- |'
) | Set-Content -Path $summaryPath -Encoding UTF8

function Invoke-LoggedDotnet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Phase,

        [Parameter(Mandatory = $true)]
        [int]$Iteration,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $label = '{0}-{1:D3}' -f $Phase, $Iteration
    $stdoutName = "$label.out.txt"
    $stderrName = "$label.err.txt"
    $stdoutPath = Join-Path $logRoot $stdoutName
    $stderrPath = Join-Path $logRoot $stderrName
    $commandLine = Format-CommandLine $Arguments
    $start = Get-Date

    & dotnet @Arguments > $stdoutPath 2> $stderrPath
    $exitCode = $LASTEXITCODE

    $end = Get-Date
    $duration = $end - $start
    $result = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
    $escapedCommand = Escape-MarkdownCell $commandLine

    Add-SummaryLine ("| {0} | {1} | `{2}` | {3} | {4} | {5:c} | {6} | {7} | {8} | {9} |" -f `
            $Phase, `
            $Iteration, `
            $escapedCommand, `
            $start.ToString('O'), `
            $end.ToString('O'), `
            $duration, `
            $exitCode, `
            $stdoutName, `
            $stderrName, `
            $result)

    if ($exitCode -ne 0) {
        Add-SummaryLine ''
        Add-SummaryLine ("Failed phase: {0}, iteration: {1}, exit code: {2}" -f $Phase, $Iteration, $exitCode)
        Add-SummaryLine ("Stdout: {0}" -f $stdoutName)
        Add-SummaryLine ("Stderr: {0}" -f $stderrName)
        exit $exitCode
    }
}

if (-not $NoBuild) {
    Invoke-LoggedDotnet `
        -Phase 'build' `
        -Iteration 1 `
        -Arguments @('build', '.\Dobby.sln', '-c', $Configuration, '--no-restore', '-warnaserror')
}

for ($i = 1; $i -le $FocusedIterations; $i++) {
    Invoke-LoggedDotnet `
        -Phase 'focused' `
        -Iteration $i `
        -Arguments @(
            'test',
            '.\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj',
            '-c',
            $Configuration,
            '--no-build',
            '--no-restore',
            '--filter',
            'FullyQualifiedName~WorkSessionShellIntegrationTests'
        )
}

for ($i = 1; $i -le $IntegrationIterations; $i++) {
    Invoke-LoggedDotnet `
        -Phase 'integration' `
        -Iteration $i `
        -Arguments @(
            'test',
            '.\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj',
            '-c',
            $Configuration,
            '--no-build',
            '--no-restore'
        )
}

for ($i = 1; $i -le $SolutionIterations; $i++) {
    Invoke-LoggedDotnet `
        -Phase 'solution' `
        -Iteration $i `
        -Arguments @(
            'test',
            '.\Dobby.sln',
            '-c',
            $Configuration,
            '--no-build',
            '--no-restore'
        )
}

$finishedAt = Get-Date
Add-SummaryLine ''
Add-SummaryLine "- Finished: $($finishedAt.ToString('O'))"
Add-SummaryLine "- Total duration: $(($finishedAt - $startedAt).ToString('c'))"
Add-SummaryLine '- Final result: PASS'

Write-Host "Flake-sensitive verification passed."
Write-Host "Summary: $summaryPath"
