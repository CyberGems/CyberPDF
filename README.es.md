# CyberPDF

<p align="center">
  <a href="./README.md">English</a> · Español
</p>

Un lector de PDF rápido y liviano para Windows. Nativo, gratuito, y hecho para
abrirse en menos de medio segundo en lugar de tardar un segundo en mostrar una
ventana.

## Descargar

Consigue la última compilación en
[releases](https://github.com/CyberGems/CyberPDF/releases/latest). El instalador no
necesita permisos de administrador y puede asociar los archivos `.pdf` con
CyberPDF; el zip portátil es una carpeta que descomprimes y ejecutas.

## Estado

Lector activo con tres temas y dos idiomas. Abre varios PDF en pestañas,
renderiza en modos continuo, página única o dos páginas, se desplaza en
vertical y horizontal, hace zoom y gira, selecciona, copia y resalta texto,
guarda marcadores personales de página, abre enlaces internos y externos, busca
con una barra flotante, imprime y recuerda el estado por documento.

## Qué funciona hoy

- Pantalla de inicio y nueva pestaña (`+` o `Ctrl+T`) con documentos recientes
  y soporte de arrastrar y soltar
- Múltiples documentos abiertos en pestañas (`Ctrl+Tab` para cambiar, `Ctrl+W`
  para cerrar pestaña)
- Abre documentos desde el diálogo, arrastrando o por línea de comandos, con
  diálogo temático de contraseña para archivos protegidos
- Modos de lectura continuo, página única y dos páginas, más un filtro nocturno
  opcional (`Alt+N`)
- Desplazamiento vertical y horizontal con barras temáticas, rueda del ratón
  (`Ctrl+Rueda` para zoom, `Shift+Rueda` en horizontal), teclado, botón central
  y herramienta mano (`Alt+H`)
- Selecciona, copia y resalta texto en cuatro colores (Amarillo, Verde, Azul y
  Rosa con `Alt+M` o desde el menú contextual; haz clic o clic derecho en el
  botón de resaltar para elegir y recordar el color activo), guardados por
  documento en `%LOCALAPPDATA%\CyberPDF\annotations.json`
- Marcadores personales por página (`Ctrl+B` o menú contextual) en el panel
  lateral sobre el índice del documento, renombrables en línea con doble clic o
  clic derecho, y marcados con una cinta en las miniaturas
- Enlaces clicables dentro del PDF (saltos internos de página y enlaces web o
  mailto)
- Píldora flotante de búsqueda arriba a la derecha (`Ctrl+F`) con opciones de
  mayúsculas (`Aa`) y palabra completa (`ab`), contador y `Enter` /
  `Shift+Enter` sin provocar saltos de diseño
- Panel lateral con miniaturas y marcadores (`F4`), borde redimensionable y
  estado recordado por documento
- Barra de estado interactiva: página anterior/siguiente, clic en el contador
  (`Ctrl+G`) para ir a cualquier página y clic en el zoom para ajustes rápidos
- Impresión (`Ctrl+P`), propiedades del documento (`Ctrl+D`) y recarga (`F5`)
- Zoom de 10% a 800%, ajustar al ancho, ajustar página completa, volver al 100%
  y rotación en pasos de 90 grados
- Tres temas (Predeterminado, Gris y Claro), cambio de idioma español/inglés en
  vivo y configuración como lector de PDF predeterminado en Ajustes (y activada
  por defecto en el instalador)
- Una sola ventana por sesión: abrir un segundo PDF lo abre en una nueva pestaña
  de la ventana activa
- Recuerda el tamaño de ventana, última página, zoom y rotación por documento

## Por qué es rápido

El motor es PDFium de Chromium, el mismo que usa Chrome, llamado directamente por
P/Invoke en lugar de a través de un framework pesado. Varias decisiones salen de
medir en vez de suponer:

- **PDFium pinta directamente en el mapa de bits que dibuja la ventana.** Los
  píxeles de cada página existen una sola vez en memoria, no dos.
- **Las páginas se generan en franjas horizontales.** Un póster se convierte en
  unas pocas reservas pequeñas en lugar de un búfer enorme.
- **Un solo hilo de render, una sola cola.** El motor no es seguro entre hilos, y
  una tubería serializada es correcta y suficientemente rápida.
- **Solo se genera lo visible**, con una pantalla de precarga.
- **Los renders obsoletos se descartan explícitamente** al cambiar el zoom o el
  tamaño, en lugar de quedarse hasta que el presupuesto de caché los expulse.

## Medido

Compilación autocontenida, sin necesidad del runtime de .NET, medida en la máquina
de desarrollo:

| | |
|---|---|
| Arranque | 0,24-0,36 s |
| Memoria, ventana vacía | 19 MB privados (59 MB residentes) |
| Memoria, documento normal | 24 MB privados (65 MB residentes) |
| Memoria, página alta extrema | 50 MB privados (91 MB residentes) |
| Descarga del instalador | 38 MB |
| Instalado | 122 MB |

Como referencia, el Foxit Reader 5.3 al que reemplaza arranca en 0,38 s y usa 64 MB.

Los detalles completos y las alternativas descartadas están en
[docs/architecture.md](docs/architecture.md).

## Compilar

Requisitos: Windows 10 1803 o posterior, .NET SDK 10, y PowerShell 5.1 o posterior.

```powershell
# Restaurar el motor nativo (descarga y verifica PDFium)
./scripts/Fetch-Pdfium.ps1

# Compilar y ejecutar
dotnet build CyberPDF.slnx -c Release
./src/CyberPDF.UI/bin/Release/net10.0-windows/CyberPDF.exe

# Ejecutar sin compilar aparte, con un documento opcional
dotnet run --project src/CyberPDF.UI/CyberPDF.UI.csproj -c Release -- documento.pdf

# Pruebas
dotnet test CyberPDF.slnx -c Release

# Compilación autocontenida en carpeta
./scripts/Publish-CyberPDF.ps1
```

## Verificar una compilación

Hay dos modos ocultos para comprobar una compilación en una máquina real:

```powershell
# Imprime métricas de diseño, franjas y memoria para un documento
CyberPDF.exe --diag documento.pdf --diag-out informe.txt

# Igual, pero se desplaza y hace zoom antes: ahí es donde aparecería una fuga
CyberPDF.exe --diag --stress documento.pdf --diag-out informe.txt

# Guarda la ventana tal como la pinta la propia aplicación
CyberPDF.exe --shot salida.png documento.pdf
```

## Atajos de teclado

| Teclas | Acción |
|---|---|
| `Ctrl+O` | Abrir un documento |
| `Ctrl++` / `Ctrl+-` / `Ctrl+Rueda` | Acercar / alejar |
| `Ctrl+0` | Tamaño real |
| `Ctrl+R` | Girar en sentido horario |
| `Alt+R` | Girar en sentido antihorario |
| `Alt+W` | Ajustar al ancho |
| `Alt+P` | Ajustar página completa |
| `Alt+H` | Alternar herramienta mano / selección |
| `Alt+N` | Alternar modo nocturno de página |
| `Alt+T` | Cambiar tema |
| `Ctrl+F` | Buscar en el documento |
| `Ctrl+G` | Ir a página |
| `Ctrl+P` | Imprimir |
| `Ctrl+D` | Propiedades del documento |
| `F4` | Miniaturas y marcadores |
| `F5` | Recargar documento |
| `F3` | Siguiente coincidencia |
| `Shift+F3` | Coincidencia anterior |
| `Ctrl+C` | Copiar la selección |
| `Ctrl+A` | Seleccionar la página actual |
| `Alt+M` | Resaltar la selección |
| `Ctrl+B` | Alternar marcador en la página actual |
| `Ctrl+T` | Abrir una nueva pestaña de inicio |
| `Ctrl+Tab` | Siguiente pestaña abierta |
| `Ctrl+W` | Cerrar pestaña activa o ventana |
| `F11` | Maximizar / restaurar |
| `Av Pág` / `Re Pág` | Desplazar una pantalla o cambiar de página |
| `Inicio` / `Fin` | Principio / final del documento |

## Licencia

GPL-3.0. PDFium y sus dependencias incluidas se distribuyen con sus propias
licencias, que acompañan a la compilación publicada.
