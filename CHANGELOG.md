# Changelog

All notable changes to CyberPDF are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/), and the project follows
[Semantic Versioning](https://semver.org/).

## [0.2.0] - 2026-11-27

### 🇺🇸 English

- Multi-document tabs above the toolbar (`Ctrl+Tab` to switch, `Ctrl+W` to
  close, and `+` / `Ctrl+T` to open a Welcome start tab with recent files)
- Continuous, single-page, and two-page reading modes, plus an optional dark
  page reading filter (`Alt+N`)
- Floating top-right search pill (`Ctrl+F`) with `Aa` match case, `ab` whole
  word toggles, match counter, custom cue-text painting without placeholder
  flicker, and zero layout jump
- Lightweight reading annotations persisted locally per document in
  `%LOCALAPPDATA%\CyberPDF\annotations.json`: four-color text highlights
  (`Alt+M` or page context menu) and personal page bookmarks (`Ctrl+B`) shown in
  the side panel and as thumbnail ribbons
- Redesigned vector icons across the chrome (including a clearer Default theme
  crescent moon), custom themed context menus on all text inputs, non-activating
  themed tooltips with shortcut chips, and live English/Spanish switching in
  Settings
- Clickable internal and external PDF links with link target previews in an
  interactive status bar (prev/next page, click page counter for `Ctrl+G` jump,
  click zoom percentage for presets)
- Document properties dialog (`Ctrl+D`), themed password dialog for encrypted
  PDFs, printing (`Ctrl+P`), reload (`F5`), horizontal scrolling with middle-drag
  and hand pan (`Alt+H`), and drag-resizable side panel

### 🇪🇸 Español

- Pestañas para múltiples documentos sobre la barra de herramientas (`Ctrl+Tab`
  para alternar, `Ctrl+W` para cerrar y `+` / `Ctrl+T` para abrir una pestaña
  de inicio con archivos recientes)
- Modos de lectura continuo, página única y dos páginas, más un filtro nocturno
  de lectura opcional (`Alt+N`)
- Píldora flotante de búsqueda arriba a la derecha (`Ctrl+F`) con botones `Aa` y
  `ab`, contador de coincidencias, texto guía sin parpadeos al pasar el puntero
  y sin saltos de diseño
- Anotaciones ligeras de lectura guardadas localmente por documento en
  `%LOCALAPPDATA%\CyberPDF\annotations.json`: resaltados de texto en cuatro
  colores (`Alt+M` o menú contextual) y marcadores personales (`Ctrl+B`) en el
  panel lateral y en las miniaturas
- Iconos vectoriales rediseñados en toda la interfaz (incluida una luna
  creciente más clara en el tema Predeterminado), menús contextuales temáticos
  en todos los campos de texto, tooltips con atajos sin pérdida de foco y cambio
  de idioma español/inglés en vivo desde Ajustes
- Enlaces internos y externos clicables con vista previa del destino en una
  barra de estado interactiva (página anterior/siguiente, salto con `Ctrl+G` y
  preajustes de zoom)
- Propiedades del documento (`Ctrl+D`), diálogo temático de contraseña,
  impresión (`Ctrl+P`), recarga (`F5`), desplazamiento horizontal con botón
  central y herramienta mano (`Alt+H`), y panel lateral redimensionable

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
  up front, and the document's bookmarks with their nesting. Its edge can be
  dragged, and its state is remembered per document
- Fit page command for documents taller than the screen, where fitting the width
  still leaves a page split across scrolls
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
  anidación. Su borde se puede arrastrar y su estado se recuerda por documento
- Comando de ajustar página para documentos más altos que la pantalla, donde
  ajustar el ancho todavía deja una página partida entre desplazamientos
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
