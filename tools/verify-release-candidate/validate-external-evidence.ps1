[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePath,
    [Parameter(Mandatory = $true)]
    [ValidateSet('installed-msix-upgrade', 'production-signing', 'license-review')]
    [string]$ExpectedGate,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedSourceCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requiredClaims = @{
    'installed-msix-upgrade' = @(
        'clean-install-n',
        'installed-launch-n',
        'upgrade-n-to-n-plus-1',
        'installed-launch-n-plus-1',
        'data-preserved',
        'runtime-root-verified',
        'uninstall-policy-verified'
    )
    'production-signing' = @(
        'msix-signature-verified',
        'certificate-chain-verified',
        'timestamp-verified',
        'publisher-identity-verified',
        'customer-content-scan-passed'
    )
    'license-review' = @('dependency-license-review-approved')
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory = $true)][psobject]$Value,
        [Parameter(Mandatory = $true)][string[]]$Allowed,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $actual = @($Value.PSObject.Properties.Name)
    $unexpected = @($actual | Where-Object { $_ -notin $Allowed })
    $missing = @($Allowed | Where-Object { $_ -notin $actual })
    if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
        throw "$Context properties do not match the v1 contract."
    }
}

$resolved = (Resolve-Path -LiteralPath $EvidencePath).Path
$document = Get-Content -LiteralPath $resolved -Raw -Encoding utf8 | ConvertFrom-Json -DateKind String -ErrorAction Stop
if ($null -eq $document -or $document -is [System.Array]) {
    throw 'External evidence must be one JSON object.'
}

Assert-ExactProperties -Value $document -Allowed @(
    'schemaVersion',
    'gate',
    'sourceCommit',
    'generatedAtUtc',
    'status',
    'claims',
    'subjects'
) -Context 'External evidence'

if ($document.schemaVersion -ne 1) {
    throw 'External evidence schemaVersion must be 1.'
}
if ([string]$document.gate -cne $ExpectedGate) {
    throw 'External evidence gate does not match the requested gate.'
}
if ([string]$document.sourceCommit -cne $ExpectedSourceCommit.ToLowerInvariant()) {
    throw 'External evidence sourceCommit does not match the candidate commit.'
}
if ([string]$document.status -cne 'passed') {
    throw 'External evidence status must be passed.'
}

$generatedAt = [DateTimeOffset]::MinValue
$generatedRaw = [string]$document.generatedAtUtc
if (-not $generatedRaw.EndsWith('Z', [StringComparison]::Ordinal) -or
    -not [DateTimeOffset]::TryParse(
        $generatedRaw,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal,
        [ref]$generatedAt) -or
    $generatedAt.Offset -ne [TimeSpan]::Zero) {
    throw 'External evidence generatedAtUtc must be a valid UTC timestamp ending in Z.'
}

if ($document.claims -isnot [System.Array]) {
    throw 'External evidence claims must be a JSON array.'
}
$claims = @($document.claims)
if ($claims.Count -eq 0 -or $claims.Count -gt 32) {
    throw 'External evidence claims count is outside the accepted range.'
}
$claimSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($claimValue in $claims) {
    $claim = [string]$claimValue
    if ([string]::IsNullOrWhiteSpace($claim) -or $claim.Length -gt 96 -or -not $claimSet.Add($claim)) {
        throw 'External evidence contains an invalid or duplicate claim.'
    }
}
foreach ($requiredClaim in $requiredClaims[$ExpectedGate]) {
    if (-not $claimSet.Contains($requiredClaim)) {
        throw "External evidence is missing required claim '$requiredClaim'."
    }
}

if ($document.subjects -isnot [System.Array]) {
    throw 'External evidence subjects must be a JSON array.'
}
$subjects = @($document.subjects)
if ($subjects.Count -eq 0 -or $subjects.Count -gt 64) {
    throw 'External evidence subjects count is outside the accepted range.'
}
$subjectNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($subject in $subjects) {
    if ($null -eq $subject -or $subject -is [string]) {
        throw 'Each external evidence subject must be an object.'
    }
    Assert-ExactProperties -Value $subject -Allowed @('name', 'sha256') -Context 'External evidence subject'
    $name = [string]$subject.name
    $sha256 = [string]$subject.sha256
    if ([string]::IsNullOrWhiteSpace($name) -or $name.Length -gt 256 -or $name -match '[\x00-\x1f\x7f]' -or -not $subjectNames.Add($name)) {
        throw 'External evidence contains an invalid or duplicate subject name.'
    }
    if ($sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'External evidence subject sha256 must be 64 lowercase hexadecimal characters.'
    }
}

[ordered]@{
    status = 'validated'
    gate = $ExpectedGate
    sourceCommit = $ExpectedSourceCommit.ToLowerInvariant()
    claimCount = $claims.Count
    subjectCount = $subjects.Count
} | ConvertTo-Json -Compress
