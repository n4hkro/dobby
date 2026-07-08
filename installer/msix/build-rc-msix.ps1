[CmdletBinding()]
param(
    [string]$MakeAppxPath,
    [string]$SignToolPath,
    [string]$PortableOutputPath,
    [switch]$SkipPublish,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot '..\..')).Path
$artifactRoot = Join-Path $repoRoot 'artifacts\packages\msix\rc'
$portablePublishScript = Join-Path $repoRoot 'tools\publish-portable\publish-portable.ps1'
$defaultPortableOutput = Join-Path $repoRoot 'artifacts\publish\portable\Dobby.Host'
$publisherSubject = 'CN=Dobby RC Self-Signed Publisher'
$publisherDisplayName = 'Dobby RC Self-Signed Publisher'
$packageName = 'Dobby'
$packageDisplayName = 'Dobby'
$versions = @('1.0.0.0', '1.0.1.0')
$requiredPortableFiles = @(
    'Dobby.Host.exe',
    'Dobby.Host.dll',
    'Dobby.Security.dll',
    'appsettings.json',
    'native\win-x64\sqlite3mc_x64.dll'
)

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$Candidate
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $candidateFull = [System.IO.Path]::GetFullPath($Candidate)
    $rootPrefix = $rootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $candidateFull.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside artifact root: $candidateFull"
    }
}

function Resolve-ToolPath {
    param(
        [string]$ExplicitPath,
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    if ($ExplicitPath) {
        $resolved = (Resolve-Path -LiteralPath $ExplicitPath).Path
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "$ToolName was not found at $ExplicitPath."
        }

        return $resolved
    }

    $candidates = @(
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64',
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64',
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64',
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64',
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64',
        'C:\Program Files (x86)\Windows Kits\10\App Certification Kit'
    ) | ForEach-Object { Join-Path $_ $ToolName }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($command -and $command.Source) {
        return $command.Source
    }

    throw "Unable to locate $ToolName. Pass -$($ToolName -replace '\.exe$','')Path explicitly."
}

function Get-ToolLabel {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($Path -match 'Windows Kits\\10\\bin\\[^\\]+\\x64\\') {
        return 'WindowsKits10-x64'
    }

    if ($Path -match 'Windows Kits\\10\\App Certification Kit\\') {
        return 'WindowsKits10-AppCertKit'
    }

    return 'ExplicitToolPath'
}

function Get-PortableOutput {
    $portableOutput = if ($PortableOutputPath) {
        (Resolve-Path -LiteralPath $PortableOutputPath).Path
    }
    else {
        $defaultPortableOutput
    }

    if (-not $SkipPublish) {
        if (-not (Test-Path -LiteralPath $portablePublishScript -PathType Leaf)) {
            throw "Portable publish script is missing at tools\publish-portable\publish-portable.ps1."
        }

        $publishArgs = @{}
        if ($NoRestore) {
            $publishArgs.NoRestore = $true
        }

        $null = & $portablePublishScript @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Portable publish script failed with exit code $LASTEXITCODE."
        }
    }

    if (-not (Test-Path -LiteralPath $portableOutput -PathType Container)) {
        throw "Portable output folder was not found."
    }

    foreach ($relativePath in $requiredPortableFiles) {
        $candidate = Join-Path $portableOutput $relativePath
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            throw "Portable output is missing required file: $relativePath"
        }
    }

    return [string]$portableOutput
}

function New-PlaceholderAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [int]$Size
    )

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 35, 92, 70))
            $fontSize = [Math]::Max([int]($Size * 0.45), 18)
            $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
            $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
            $format = New-Object System.Drawing.StringFormat
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            try {
                $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
                $graphics.DrawString('D', $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $Size, $Size), $format)
            }
            finally {
                $format.Dispose()
                $brush.Dispose()
                $font.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function Get-OrCreateCertificate {
    $existing = Get-ChildItem -Path Cert:\CurrentUser\My |
        Where-Object {
            $_.Subject -eq $publisherSubject -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date) -and
            $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3'
        } |
        Sort-Object NotBefore -Descending |
        Select-Object -First 1

    if ($existing) {
        return $existing
    }

    return New-SelfSignedCertificate `
        -Subject $publisherSubject `
        -Type CodeSigningCert `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -KeyExportPolicy NonExportable `
        -HashAlgorithm 'SHA256' `
        -NotAfter (Get-Date).AddYears(2)
}

function New-AppxManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity
    Name="$packageName"
    Publisher="$publisherSubject"
    Version="$Version"
    ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>$packageDisplayName</DisplayName>
    <PublisherDisplayName>$publisherDisplayName</PublisherDisplayName>
    <Description>$packageDisplayName RC MSIX package</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.18362.0" />
  </Dependencies>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
  <Applications>
    <Application Id="DobbyHost" Executable="Dobby.Host.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="$packageDisplayName"
        Description="$packageDisplayName"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" Square71x71Logo="Assets\Square71x71Logo.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
</Package>
"@

    [System.IO.File]::WriteAllText($ManifestPath, $manifest, [System.Text.UTF8Encoding]::new($false))
}

function New-PackageStage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PortableOutput,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $stageRoot = Join-Path $artifactRoot "stage\$Version"
    Assert-ChildPath -Root $artifactRoot -Candidate $stageRoot

    if (Test-Path -LiteralPath $stageRoot) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
    Get-ChildItem -LiteralPath $PortableOutput -Force | Copy-Item -Destination $stageRoot -Recurse -Force

    $assetsRoot = Join-Path $stageRoot 'Assets'
    New-Item -ItemType Directory -Force -Path $assetsRoot | Out-Null

    New-PlaceholderAsset -Path (Join-Path $assetsRoot 'StoreLogo.png') -Size 50
    New-PlaceholderAsset -Path (Join-Path $assetsRoot 'Square44x44Logo.png') -Size 44
    New-PlaceholderAsset -Path (Join-Path $assetsRoot 'Square71x71Logo.png') -Size 71
    New-PlaceholderAsset -Path (Join-Path $assetsRoot 'Square150x150Logo.png') -Size 150
    New-PlaceholderAsset -Path (Join-Path $assetsRoot 'Wide310x150Logo.png') -Size 310

    New-AppxManifest -ManifestPath (Join-Path $stageRoot 'AppxManifest.xml') -Version $Version
    return $stageRoot
}

function Convert-ToRepoRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $uriRoot = [System.Uri](([System.IO.Path]::GetFullPath($repoRoot)).TrimEnd('\') + '\')
    $uriPath = [System.Uri]([System.IO.Path]::GetFullPath($Path))
    return [System.Uri]::UnescapeDataString($uriRoot.MakeRelativeUri($uriPath).ToString()).Replace('/', '\')
}

$makeAppxExe = Resolve-ToolPath -ExplicitPath $MakeAppxPath -ToolName 'makeappx.exe'
$signToolExe = Resolve-ToolPath -ExplicitPath $SignToolPath -ToolName 'signtool.exe'
$portableOutput = Get-PortableOutput
$certificate = Get-OrCreateCertificate

Assert-ChildPath -Root (Join-Path $repoRoot 'artifacts\packages') -Candidate $artifactRoot
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$packageResults = @()

foreach ($version in $versions) {
    $stageRoot = New-PackageStage -PortableOutput $portableOutput -Version $version
    $packagePath = Join-Path $artifactRoot ("Dobby_{0}_x64_RC.msix" -f $version)
    Assert-ChildPath -Root $artifactRoot -Candidate $packagePath

    & $makeAppxExe pack /d $stageRoot /p $packagePath /o
    if ($LASTEXITCODE -ne 0) {
        throw "MakeAppx failed for version $version with exit code $LASTEXITCODE."
    }

    & $signToolExe sign /fd SHA256 /sha1 $certificate.Thumbprint $packagePath
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool failed for version $version with exit code $LASTEXITCODE."
    }

    $hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $packageResults += [pscustomobject]@{
        version = $version
        packagePath = Convert-ToRepoRelativePath -Path $packagePath
        sha256 = $hash
    }
}

$summary = [pscustomobject]@{
    packageIdentity = $packageName
    displayName = $packageDisplayName
    publisher = $publisherSubject
    publisherDisplayName = $publisherDisplayName
    certificate = [pscustomobject]@{
        subject = $certificate.Subject
        thumbprint = $certificate.Thumbprint
        store = 'CurrentUser\My'
    }
    tooling = [pscustomobject]@{
        makeAppx = [pscustomobject]@{
            label = Get-ToolLabel -Path $makeAppxExe
            fileVersion = (Get-Item -LiteralPath $makeAppxExe).VersionInfo.FileVersion
        }
        signTool = [pscustomobject]@{
            label = Get-ToolLabel -Path $signToolExe
            fileVersion = (Get-Item -LiteralPath $signToolExe).VersionInfo.FileVersion
        }
    }
    portableOutput = Convert-ToRepoRelativePath -Path $portableOutput
    packages = $packageResults
}

$summaryPath = Join-Path $artifactRoot 'summary.json'
$shaPath = Join-Path $artifactRoot 'sha256.txt'

$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding utf8
$packageResults | ForEach-Object { "{0}  {1}" -f $_.sha256, $_.packagePath } | Set-Content -LiteralPath $shaPath -Encoding utf8

Write-Host "MSIX RC packages:"
foreach ($package in $packageResults) {
    Write-Host (" - {0} [{1}]" -f $package.packagePath, $package.sha256)
}
Write-Host ("Summary: {0}" -f (Convert-ToRepoRelativePath -Path $summaryPath))
