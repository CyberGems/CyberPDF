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
- Text selection with copy: drag to select, double click for a word, triple click
  for a line, Ctrl+C to copy, and a themed context menu on the page
- Search across the document with Ctrl+F: every match is highlighted, the active
  one in a stronger color, F3 and Shift+Enter walk the hits and the view scrolls
  to each one
- Side panel with page thumbnails, generated around the visible page instead of
  up front, and the document's bookmarks with their nesting
- Window chrome with minimize, maximize and close, drag by the native title strip
  (Snap included), and themed tooltips that show each shortcut
- One window per session: opening another PDF in Explorer hands it to the running
  window instead of starting a second copy
- Inno Setup installer with optional `.pdf` association, both languages and no
  administrator rights required
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
- Selección de texto con copia: arrastra para seleccionar, doble clic para una
  palabra, triple clic para una línea, Ctrl+C para copiar, y un menú contextual
  temático sobre la página
- Búsqueda en todo el documento con Ctrl+F: cada coincidencia se resalta y la
  activa en un color más fuerte, F3 y Shift+Enter recorren los resultados y la
  vista se desplaza hasta cada uno
- Panel lateral con miniaturas de las páginas, generadas alrededor de la página
  visible en lugar de todas de una vez, y los marcadores del documento con su
  anidación
- Barra de ventana con minimizar, maximizar y cerrar, arrastre por la franja
  nativa del título (incluye Snap) y tooltips temáticos con cada atajo
- Una sola ventana por sesión: abrir otro PDF desde el Explorador lo entrega a la
  ventana activa en lugar de iniciar una segunda copia
- Instalador Inno Setup con asociación `.pdf` opcional, ambos idiomas y sin
  necesidad de administrador
- Las páginas se generan en franjas horizontales, directamente sobre el mapa de
  bits de la ventana, así los píxeles se guardan una sola vez y una página alta
  nunca necesita un búfer enorme
- Modos ocultos `--diag`, `--diag --stress` y `--shot` para verificar una
  compilación y su comportamiento de memoria en una máquina real
