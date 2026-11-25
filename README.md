# CyberPDF

<p align="center">
  English · <a href="./README.es.md">Español</a>
</p>

A fast, lightweight PDF reader for Windows. Native, free, and built to start in
under half a second instead of taking a second to show a window.

## Download

Get the latest build from [releases](https://github.com/CyberGems/CyberPDF/releases/latest).
The installer needs no administrator rights and can associate `.pdf` files with
CyberPDF; the portable zip is a folder you unzip and run.

## Status

Phase 0: a working reader. It opens a PDF, renders it continuously, scrolls,
zooms and rotates, selects and copies text, and searches with highlighting, in
three themes and two languages. Tabs, thumbnails, bookmarks and printing are not
implemented yet.

## What works today

- Opens a document by dialog, drag and drop, or the command line
- Continuous vertical scrolling with a themed scrollbar, mouse wheel, keyboard
  and middle-button panning
- Select and copy text: drag, double click for a word, triple click for a line
- Search the whole document with every match highlighted and the active one in
  a stronger color; `Ctrl+F` opens the bar, `Enter` and `Shift+Enter` walk matches
- A side panel with page thumbnails and the document's bookmarks, `F4` to show
  it, drag its edge to resize; click either to jump to that page
- The panel, its tab and its width come back on the next launch, like the theme
- One window per session: opening a second PDF hands it to the running window
- Zoom from 10% to 800%, fit width, 100% reset
- Rotation in 90 degree steps
- Three themes: Default, Gray and Light
- English and Spanish interface
- Remembers the last page, zoom and rotation per document, and recent files
- Password protected documents are detected and reported

## Why it is fast

The engine is Chromium's PDFium, the same one Chrome uses, called directly
through P/Invoke instead of through a heavy framework. Several decisions follow
from measuring rather than guessing:

- **PDFium paints straight into the bitmap the window draws.** Page pixels exist
  once in memory, not twice.
- **Pages render in horizontal bands.** A poster-sized page becomes a few small
  allocations instead of one huge buffer.
- **One render thread, one queue.** The engine is not thread-safe, and a single
  serialized pipeline is both correct and fast enough.
- **Only what is visible gets rendered**, with one viewport of prefetch.
- **Stale renders are dropped explicitly** when zoom or size changes, instead of
  lingering until a cache budget evicts them.

## Measured

Self-contained build, no .NET runtime needed, measured on the development machine:

| | |
|---|---|
| Startup | 0.24-0.36 s |
| Memory, empty window | 19 MB private (59 MB working set) |
| Memory, ordinary document | 24 MB private (65 MB working set) |
| Memory, worst-case tall page | 50 MB private (91 MB working set) |
| Installer download | 38 MB |
| Installed | 122 MB |

For reference, the Foxit Reader 5.3 this replaces starts in 0.38 s and uses 64 MB.

See [docs/architecture.md](docs/architecture.md) for the full measurements and
the alternatives that were rejected.

## Building

Requirements: Windows 10 1803 or later, .NET SDK 10, and PowerShell 5.1 or later.

```powershell
# Restore the native engine (downloads and verifies PDFium)
./scripts/Fetch-Pdfium.ps1

# Build and run
dotnet build CyberPDF.slnx -c Release
./src/CyberPDF.UI/bin/Release/net10.0-windows/CyberPDF.exe

# Run without building separately, optionally opening a document
dotnet run --project src/CyberPDF.UI/CyberPDF.UI.csproj -c Release -- document.pdf

# Tests
dotnet test CyberPDF.slnx -c Release

# Self-contained folder build
./scripts/Publish-CyberPDF.ps1
```

## Verifying a build

Two hidden modes exist for checking a build on a real machine:

```powershell
# Print layout, band and memory metrics for a document
CyberPDF.exe --diag document.pdf --diag-out report.txt

# Same, but scroll and zoom first, which is where a leak would show
CyberPDF.exe --diag --stress document.pdf --diag-out report.txt

# Save the window as the app paints it
CyberPDF.exe --shot out.png document.pdf
```

## Keyboard shortcuts

| Keys | Action |
|---|---|
| `Ctrl+O` | Open a document |
| `Ctrl++` / `Ctrl+-` | Zoom in / out |
| `Ctrl+0` | Actual size |
| `Ctrl+R` | Rotate clockwise |
| `Alt+R` | Rotate counterclockwise |
| `Alt+W` | Fit width |
| `Alt+T` | Change theme |
| `Ctrl+F` | Search in the document |
| `F4` | Thumbnails and bookmarks |
| `F3` | Next match |
| `Shift+F3` | Previous match |
| `Ctrl+C` | Copy the selection |
| `Ctrl+A` | Select the current page |
| `Ctrl+W` | Close |
| `F11` | Maximize / restore |
| `Page Up` / `Page Down` | Scroll a screen |
| `Home` / `End` | Start / end of document |

## License

GPL-3.0. PDFium and its bundled dependencies ship under their own licenses,
included with the published build.
