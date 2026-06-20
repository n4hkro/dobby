param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$adminExe = Join-Path $repoRoot 'src\Dobby.Admin\bin\Release\net10.0-windows\Dobby.Admin.exe'
if (-not (Test-Path -LiteralPath $adminExe)) {
    $adminExe = Join-Path $repoRoot 'src\Dobby.Admin\bin\Debug\net10.0-windows\Dobby.Admin.exe'
}

if (-not (Test-Path -LiteralPath $adminExe)) {
    [Console]::Error.WriteLine('admin-executable-not-found')
    exit 70
}

& $adminExe --cli create-recovery-response @Arguments
exit $LASTEXITCODE
