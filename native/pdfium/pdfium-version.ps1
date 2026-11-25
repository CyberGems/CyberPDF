# Pinned PDFium build for CyberPDF.
#
# The engine is Chrome's PDFium, distributed as prebuilt binaries by the
# bblanchon/pdfium-binaries project. These builds are compiled without V8 and
# without XFA support, which removes the two largest attack surfaces in the
# PDF format and keeps the DLL small.
#
# Do not change these values without re-fetching, re-verifying the checksum and
# re-running the render benchmarks in docs/architecture.md.
$PdfiumVersion = 'chromium/8179'
$PdfiumPdfiumVersion = '160.0.8179.0'

# SHA-256 of each release archive, computed from the downloaded files.
$PdfiumHashes = @{
    'win-x64' = '836a77c9053f624839c66010cddea7b675628a36b8831d2330178bb169d241b6'
    # Remaining architectures stay disabled until their archives are verified.
    # 'win-x86'     = ''
    # 'win-arm64'   = ''
}
