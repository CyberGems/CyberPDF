# CyberPDF architecture

Phase 0 skeleton: the decisions that govern the code, and the measurements they
were based on. Every number here was measured on the development machine, not
estimated.

## Why this shape

The project exists because existing readers feel slow or heavy. Three candidate
shells were built and measured with the same page, before any product code was
written:

| Shell, empty window | Startup | Working set | Private |
|---|---|---|---|
| WinForms | 0.57-0.86 s | 51 MB | 16 MB |
| WPF | 1.16-1.46 s | 92 MB | 61 MB |
| Electron | ~0.5 s | 290 MB | 150 MB |

WinForms won on the two metrics that matter for a reader. WPF costs roughly
double the startup and four times the private memory for the same content, and
Electron has a floor of ~290 MB before a document is even open. Foxit Reader 5.3,
the reference point, starts in 0.38 s and uses 64 MB.

## Engine

Chromium's PDFium, prebuilt by `bblanchon/pdfium-binaries`, compiled without V8
and without XFA. Both omissions remove large attack surface, and the DLL is
7.5 MB.

Measured with a real 300-page document and a set of real-world PDFs:

| Operation | Time |
|---|---|
| Open a 300-page document | 1.6 ms |
| Geometry for 300 pages | 13.7 ms |
| Render an A4 page at 125% (warm) | 5.5 ms |
| Render 60 pages at 100% | 213 ms |
| Text layer, ~3,300 characters | 2.6 ms |

The engine is never the bottleneck. What makes a reader feel slow is everything
built on top of it, which is why this project spends its effort on the render
pipeline instead.

Rejected alternatives: managed PDFium wrappers (stale engine, no text layer),
PDFsharp (writer only), MuPDF (no usable .NET binding), `Windows.Data.Pdf` (no
text, links or forms), Electron with PDF.js (wrong footprint).

## Render pipeline

1. One thread, one queue (`PdfEngine`). PDFium is not thread-safe.
2. Documents are opened by streaming with `FPDF_LoadCustomDocument`, so a large
   file is not held in memory.
3. Geometry for every page is resolved up front, which is what lets the scroll
   space exist before anything renders.
4. Renders are split into horizontal bands of at most 1280 pixels. A tall page
   costs several small allocations instead of one large one.
5. PDFium paints directly into the GDI+ bitmap (`FPDFBitmap_CreateEx`), so page
   pixels exist exactly once.
6. Rises and falls of zoom, rotation and size change the render geometry. The
   previous generation is dropped explicitly instead of waiting for the byte
   budget to evict it; without that, stale bands kept about 100 MB resident.

## Measured results of the final build

Self-contained folder publish (no .NET runtime required):

| Scenario | Startup | Working set | Private |
|---|---|---|---|
| Empty window | ~0.24 s warm | 59 MB | 19 MB |
| Ordinary one-page document | ~0.24 s | 65 MB | 24 MB |
| One page 1,200 x 4,069 pt (worst case) | ~0.36 s | 91 MB | 50 MB |

The worst-case document holds 4 bands, 27 MB of pixels, against a 64 MB cache
budget. After three full scroll passes and six zoom changes the process returns to
a steady state instead of accumulating.

### Single-file packaging costs memory

The same app published as a self-contained single file measured 82 MB private
with an empty window and 114 MB with a document open, against 19 MB and 50 MB in
a folder publish. The single-file host keeps the extracted runtime mappings
alive. Since the suite already ships folder publishes and an installer compresses
them, the single-file bundle was dropped.

## What is not implemented yet

Phase 0 is a reader skeleton. Missing, in rough order of value:

- Tabs
- Search with highlighting
- Text selection and copy (the text layer exists and is tested)
- Thumbnails and bookmarks
- Password entry for protected documents (detection already maps the error)
- Links
- Printing
- Installer and updater
