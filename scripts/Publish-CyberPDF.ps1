<#
.SYNOPSIS
    Publishes CyberPDF as a self-contained folder build.

.DESCRIPTION
    Produces a build that runs without the .NET runtime installed, matching the
    rest of the suite. It is a folder rather than a single-file bundle because
    the single-file host measured four times the private memory on an empty
    window. The native PDFium DLL is copied beside the executable, since a
    native library cannot live inside a managed bundle without extracting it on
    every launch.

.PARAMETER OutputDirectory
    Where the published build is written. Defaults to publish-win64.

.PARAMETER Architecture
    Target runtime identifier. Only win-x64 is verified for now.

.EXAMPLE
    ./scripts/Publish-CyberPDF.ps1
    ./scripts/Publish-CyberPDF.ps1 -OutputDirectory ./dist
#>
[CmdletBinding()]
param(
    # Empty means publish-win64 next to the repository root. The default cannot
    # use $PSScriptRoot directly: parameter defaults are evaluated before the
    # automatic variable is populated.
    [string]$OutputDirectory = '',

    [ValidateSet('win-x64')]
    [string]$Architecture = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'publish-win64'
}
$project = Join-Path $repoRoot 'src/CyberPDF.UI/CyberPDF.UI.csproj'
$outputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$nativeDll = Join-Path $repoRoot 'native/pdfium/bin/pdfium.dll'

if (-not (Test-Path $nativeDll)) {
    throw "PDFium is missing. Run scripts/Fetch-Pdfium.ps1 first."
}

if (Test-Path $outputPath) {
    Remove-Item -Path $outputPath -Recurse -Force
}

Write-Host "Publishing CyberPDF ($Architecture)..." -ForegroundColor Cyan

# Self-contained, but as a normal folder rather than a single-file bundle.
# Measured on this app: the single-file host raised private memory from 19 MB to
# 82 MB on an empty window (49 MB to 113 MB with a document open) because the
# bundled host keeps the extracted runtime mappings alive. A folder publish has
# the same no-runtime-needed property, the same suite-wide layout, and a quarter
# of the memory. Startup is comparable.
dotnet publish $project -c Release -r $Architecture --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -p:EnableCompressionInSingleFile=false `
    -o $outputPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed (exit code $LASTEXITCODE)."
}

# The engine and its licenses must ship next to the executable.
Copy-Item -Path $nativeDll -Destination (Join-Path $outputPath 'pdfium.dll') -Force
$licenseSource = Join-Path $repoRoot 'native/pdfium/licenses'
if (Test-Path $licenseSource) {
    $licenseTarget = Join-Path $outputPath 'licenses/pdfium'
    New-Item -ItemType Directory -Path $licenseTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $licenseSource '*') -Destination $licenseTarget -Force
}
$topLicense = Join-Path $repoRoot 'native/pdfium/LICENSE-pdfium-binaries.txt'
if (Test-Path $topLicense) {
    Copy-Item -Path $topLicense -Destination (Join-Path $outputPath 'licenses/pdfium-binaries.txt') -Force
}

# Sanity checks: the exe exists, the runtime is beside it, and the engine is
# beside it. A publish that silently produced a broken folder is worse than a
# failed publish.
$exe = Join-Path $outputPath 'CyberPDF.exe'
if (-not (Test-Path $exe)) {
    throw "Publish finished but CyberPDF.exe is missing."
}
# A self-contained publish must carry the runtime; without these the app would
# silently depend on a machine-wide .NET installation.
foreach ($required in @('coreclr.dll', 'hostfxr.dll')) {
    if (-not (Test-Path (Join-Path $outputPath $required))) {
        throw "Publish is not self-contained: $required is missing."
    }
}
if (-not (Test-Path (Join-Path $outputPath 'pdfium.dll'))) {
    throw "Publish is missing the native engine (pdfium.dll)."
}

$exeSize = (Get-Item $exe).Length / 1MB
$totalSize = (Get-ChildItem -Path $outputPath -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
$dllSize = (Get-Item (Join-Path $outputPath 'pdfium.dll')).Length / 1MB
Write-Host ''
Write-Host 'Published:' -ForegroundColor Green
Write-Host ("  {0}" -f $exe) -ForegroundColor DarkGray
Write-Host ("  CyberPDF.exe : {0:N1} MB (self-contained, folder)" -f $exeSize) -ForegroundColor DarkGray
Write-Host ("  pdfium.dll   : {0:N1} MB" -f $dllSize) -ForegroundColor DarkGray
Write-Host ("  total folder : {0:N1} MB (installer compresses this)" -f $totalSize) -ForegroundColor DarkGray
