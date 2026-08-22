[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PortableRoot,
    [string]$MigrationFixturePath,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$portableFull = (Resolve-Path -LiteralPath $PortableRoot).Path
$manifestPath = Join-Path $portableFull 'artifact-manifest.json'
$executablePath = Join-Path $portableFull 'Dobby.Host.exe'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw 'Portable root does not contain the manifest and executable.'
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.artifactMode -ne 'PortableSupport' -or $manifest.runtimeIdentifier -ne 'win-x64') {
    throw 'Portable manifest mode/runtime is not approved for launch verification.'
}

$tempRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) ('dobby-portable-launch-' + [Guid]::NewGuid().ToString('N'))))
$expectedPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\dobby-portable-launch-'
if (-not $tempRoot.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to use a launch-test data root outside the approved temporary prefix.'
}

New-Item -ItemType Directory -Path $tempRoot | Out-Null
$process = $null
try {
    if ($MigrationFixturePath) {
        $fixtureFull = (Resolve-Path -LiteralPath $MigrationFixturePath).Path
        Get-ChildItem -LiteralPath $fixtureFull -Force | Copy-Item -Destination $tempRoot -Recurse -Force
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($executablePath)
    $startInfo.WorkingDirectory = $portableFull
    $startInfo.UseShellExecute = $false
    $startInfo.Environment['DOBBY__Runtime__DataRoot'] = $tempRoot
    $startInfo.Environment['DOBBY__Product__ArtifactMode'] = 'PortableSupport'
    $launchProcess = [System.Diagnostics.Process]::new()
    $launchProcess.StartInfo = $startInfo
    if (-not $launchProcess.Start()) {
        $launchProcess.Dispose()
        throw 'Portable executable did not start.'
    }
    $process = $launchProcess

    if (-not $process.WaitForInputIdle(30000)) {
        throw 'Portable executable did not reach an interactive login window.'
    }

    if ($process.HasExited) {
        throw "Portable executable exited before verification with code $($process.ExitCode)."
    }

    if (-not $process.CloseMainWindow()) {
        throw 'Portable login window did not accept a normal close request.'
    }

    if (-not $process.WaitForExit(15000)) {
        throw 'Portable executable did not exit cleanly after its login window closed.'
    }

    if ($process.ExitCode -ne 0) {
        throw "Portable executable returned exit code $($process.ExitCode)."
    }

    $dataFiles = @(
        Get-ChildItem -LiteralPath $tempRoot -Recurse -File |
            ForEach-Object { [System.IO.Path]::GetRelativePath($tempRoot, $_.FullName).Replace('\', '/') } |
            Sort-Object
    )
    if ($dataFiles.Count -eq 0) {
        throw 'Portable launch did not initialize its isolated support data root.'
    }

    if ($ReportPath) {
        $report = [ordered]@{
            schemaVersion = 1
            artifactId = $manifest.artifactId
            executableSha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant()
            artifactMode = $manifest.artifactMode
            migrationFixtureUsed = -not [string]::IsNullOrWhiteSpace($MigrationFixturePath)
            loginWindowReached = $true
            cleanExit = $true
            isolatedDataFiles = $dataFiles
        }
        $parent = Split-Path -Parent ([System.IO.Path]::GetFullPath($ReportPath))
        if ($parent) {
            New-Item -ItemType Directory -Force -Path $parent | Out-Null
        }
        $report | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $ReportPath -Encoding utf8NoBOM
    }

    Write-Host "Portable launch verification passed: $($manifest.artifactId)"
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit()
    }
    if ($process) {
        $process.Dispose()
    }

    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
