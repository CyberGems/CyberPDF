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

Active reader in three themes and two languages. It opens multiple PDFs in
tabs, renders in continuous, single-page or two-page layouts, scrolls
vertically and horizontally, zooms and rotates, selects and copies text, clicks
internal and external links, searches with highlighting, prints, and remembers
state per document.

## What works today

- Start screen with a recent documents list and drag-and-drop support
- Multiple open documents in tabs (`Ctrl+Tab` to switch, `Ctrl+W` to close tab)
- Opens a document by dialog, drag and drop, or the command line, with a
  themed password prompt for protected files
- Continuous, single-page and two-page reading modes, plus an optional dark
  page filter (`Alt+N`)
- Smooth vertical and horizontal scrolling with themed scrollbars, mouse wheel
  (`Ctrl+Wheel` to zoom, `Shift+Wheel` horizontally), keyboard, middle-drag and
  hand pan tool (`Alt+H`)
- Select and copy text: drag, double click for a word, triple click for a line,
  themed context menus with icons
- Clickable PDF links (internal page jumps and external web or mailto links)
- Search the whole document with match case and whole word toggles; `Ctrl+F`
  opens the bar, `Enter` and `Shift+Enter` walk matches
- Side panel with page thumbnails and document bookmarks (`F4`), resizable edge
  and remembered state per document
- Interactive status bar: previous/next page, click page counter (`Ctrl+G`) to
  jump to any page, click zoom percentage for presets
- Printing (`Ctrl+P`), document properties (`Ctrl+D`) and reload (`F5`)
- Zoom from 10% to 800%, fit width, fit whole page, 100% reset, and rotation in
  90 degree steps
- Three themes (Default, Gray, Light) and live English/Spanish switching in
  Settings
- One window per session: opening a second PDF hands it to a new tab in the
  running window
- Remembers window bounds, last page, zoom and rotation per document

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
| `Ctrl++` / `Ctrl+-` / `Ctrl+Wheel` | Zoom in / out |
| `Ctrl+0` | Actual size |
| `Ctrl+R` | Rotate clockwise |
| `Alt+R` | Rotate counterclockwise |
| `Alt+W` | Fit width |
| `Alt+P` | Fit whole page |
| `Alt+H` | Toggle hand / select tool |
| `Alt+N` | Toggle dark page mode |
| `Alt+T` | Change theme |
| `Ctrl+F` | Search in the document |
| `Ctrl+G` | Go to page |
| `Ctrl+P` | Print |
| `Ctrl+D` | Document properties |
| `F4` | Thumbnails and bookmarks |
| `F5` | Reload document |
| `F3` | Next match |
| `Shift+F3` | Previous match |
| `Ctrl+C` | Copy the selection |
| `Ctrl+A` | Select the current page |
| `Ctrl+Tab` | Next open tab |
| `Ctrl+W` | Close active tab or window |
| `F11` | Maximize / restore |
| `Page Up` / `Page Down` | Scroll a screen or step page |
| `Home` / `End` | Start / end of document |

## License

GPL-3.0. PDFium and its bundled dependencies ship under their own licenses,
included with the published build.
