# Third-party notices

## PDFium

This application uses PDFium, the PDF rendering engine from the Chromium project.

- Project: https://pdfium.googlesource.com/pdfium/
- Prebuilt binaries: https://github.com/bblanchon/pdfium-binaries
- License: BSD-3-Clause, plus the licenses of its bundled dependencies

The version in use is pinned in `native/pdfium/pdfium-version.ps1` and verified by
SHA-256 when restored. The complete set of license texts ships with every
published build under `licenses/pdfium`, and the license of the binary packaging
itself as `licenses/pdfium-binaries.txt`.

The PDFium build used here is compiled without the V8 JavaScript engine and
without XFA support. Those features are intentionally absent, not missing by
accident: they are the two largest attack surfaces in the format.

## Bundled dependencies of PDFium

The `licenses/pdfium` folder contains the license of every library linked into
pdfium.dll, including FreeType, libjpeg-turbo, libpng, libopenjpeg, lcms, zlib,
ICU, HarfBuzz, Abseil and the LLVM libc subset. Those texts are redistributed
unchanged with the binary.
