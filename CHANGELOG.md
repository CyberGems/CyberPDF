# Changelog

All notable changes to CyberPDF are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-11-25

### 🇺🇸 English

- Initial reader skeleton built on Chromium's PDFium through direct P/Invoke
- Continuous vertical scrolling with themed scrollbar, mouse wheel, keyboard and
  drag-to-pan
- Zoom from 10% to 800%, fit width and actual size
- Page rotation in 90 degree steps
- Three themes: Default, Gray and Light
- English and Spanish interface
- Recent files, and page, zoom and rotation restored per document
- Password protected documents are detected and reported with a clear message
- Pages render in horizontal bands, straight into the window bitmap, so pixels
  are stored once and a tall page never needs a single huge buffer
- Hidden `--diag`, `--diag --stress` and `--shot` modes for verifying a build and
  its memory behavior on a real machine

### 🇪🇸 Español

- Esqueleto inicial del lector construido sobre PDFium de Chromium mediante
  P/Invoke directo
- Desplazamiento vertical continuo con barra temática, rueda del ratón, teclado y
  arrastre
- Zoom de 10% a 800%, ajustar al ancho y tamaño real
- Rotación de página en pasos de 90 grados
- Tres temas: Predeterminado, Gris y Claro
- Interfaz en inglés y español
- Archivos recientes, y página, zoom y rotación restaurados por documento
- Los documentos protegidos con contraseña se detectan y se informan con un
  mensaje claro
- Las páginas se generan en franjas horizontales, directamente sobre el mapa de
  bits de la ventana, así los píxeles se guardan una sola vez y una página alta
  nunca necesita un búfer enorme
- Modos ocultos `--diag`, `--diag --stress` y `--shot` para verificar una
  compilación y su comportamiento de memoria en una máquina real
