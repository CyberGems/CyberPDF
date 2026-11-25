<#
.SYNOPSIS
    Downloads and verifies the pinned PDFium native binaries for CyberPDF.

.DESCRIPTION
    Fetches the prebuilt PDFium archive for the requested architecture from the
    pinned release, validates its SHA-256 against native/pdfium/pdfium-version.ps1
    and extracts pdfium.dll together with its license files into native/pdfium/.
    The DLL is intentionally not committed: it is restored on demand, so the
    repository stays small and every binary is traceable to a release and hash.

.PARAMETER Architecture
    Target architecture. Only win-x64 is currently pinned and verified.

.PARAMETER Force
    Re-download and re-extract even when the DLL already exists.

.EXAMPLE
    ./scripts/Fetch-Pdfium.ps1
    ./scripts/Fetch-Pdfium.ps1 -Force
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Architecture = 'win-x64',

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $repoRoot 'native/pdfium/pdfium-version.ps1'
$targetDir = Join-Path $repoRoot 'native/pdfium'
$binDir = Join-Path $targetDir 'bin'
$dllPath = Join-Path $binDir 'pdfium.dll'

if (-not (Test-Path $versionFile)) {
    throw "Version file not found: $versionFile"
}
. $versionFile

if ($PdfiumHashes.Keys -notcontains $Architecture) {
    throw "Architecture '$Architecture' has no pinned archive yet. Add its SHA-256 to native/pdfium/pdfium-version.ps1 first."
}

$expectedHash = $PdfiumHashes[$Architecture]
$encodedVersion = $PdfiumVersion.Replace('/', '%2F')
$archiveName = "pdfium-$Architecture.tgz"
$url = "https://github.com/bblanchon/pdfium-binaries/releases/download/$encodedVersion/$archiveName"

if ((Test-Path $dllPath) -and -not $Force) {
    $existing = Get-FileHash -Path $dllPath -Algorithm SHA256
    Write-Host "PDFium already present ($Architecture, $($existing.Hash.Substring(0, 12))...)." -ForegroundColor DarkGray
    Write-Host 'Use -Force to re-download.' -ForegroundColor DarkGray
    return
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cyberpdf-pdfium-" + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $tempRoot $archiveName
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    Write-Host "Downloading PDFium $PdfiumPdfiumVersion ($Architecture)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing

    $actualHash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Checksum mismatch for $archiveName.`n  expected: $expectedHash`n  actual:   $actualHash"
    }
    Write-Host "Checksum verified: $($actualHash.Substring(0, 16))..." -ForegroundColor Green

    Write-Host 'Extracting...' -ForegroundColor Cyan
    # Use the Windows tar component explicitly. A tar.exe from Git for Windows or
    # MSYS on PATH treats "C:\..." as a remote host and fails.
    $windowsTar = Join-Path $env:SystemRoot 'System32/tar.exe'
    if (-not (Test-Path $windowsTar)) {
        throw "Windows tar not found at $windowsTar. Windows 10 1803 or later is required."
    }
    & $windowsTar -xzf $archivePath -C $tempRoot
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with exit code $LASTEXITCODE."
    }

    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    $extractedDll = Get-ChildItem -Path $tempRoot -Recurse -Filter 'pdfium.dll' | Select-Object -First 1
    if (-not $extractedDll) {
        throw 'pdfium.dll was not found in the downloaded archive.'
    }
    Copy-Item -Path $extractedDll.FullName -Destination $dllPath -Force

    # Headers are kept next to the DLL so the interop signatures can be checked
    # against the exact build in use instead of a remembered version.
    $includeSource = Join-Path $tempRoot 'include'
    if (Test-Path $includeSource) {
        $includeTarget = Join-Path $targetDir 'include'
        if (Test-Path $includeTarget) { Remove-Item -Path $includeTarget -Recurse -Force }
        Copy-Item -Path $includeSource -Destination $includeTarget -Recurse -Force
    }

    # Licenses ship with the binary: PDFium itself plus its bundled dependencies.
    # They are required for redistribution, so they are copied next to the DLL and
    # packaged by the installer.
    $licenseSource = Join-Path $tempRoot 'licenses'
    if (Test-Path $licenseSource) {
        $licenseTarget = Join-Path $targetDir 'licenses'
        if (Test-Path $licenseTarget) { Remove-Item -Path $licenseTarget -Recurse -Force }
        Copy-Item -Path $licenseSource -Destination $licenseTarget -Recurse -Force
    }
    $topLicense = Join-Path $tempRoot 'LICENSE'
    if (Test-Path $topLicense) {
        Copy-Item -Path $topLicense -Destination (Join-Path $targetDir 'LICENSE-pdfium-binaries.txt') -Force
    }

    $size = (Get-Item $dllPath).Length
    Write-Host ''
    Write-Host ("PDFium ready: {0}" -f $dllPath) -ForegroundColor Green
    Write-Host ("  version: {0} ({1})" -f $PdfiumPdfiumVersion, $PdfiumVersion) -ForegroundColor DarkGray
    Write-Host ("  size:    {0:N1} MB" -f ($size / 1MB)) -ForegroundColor DarkGray
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
