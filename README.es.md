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

Fase 0: un lector funcional. Abre un PDF, lo muestra de forma continua, permite
desplazarse, hacer zoom y girar, selecciona y copia texto, y busca con resaltado,
con tres temas e interfaz en dos idiomas. Pestañas, miniaturas, marcadores e
impresión todavía no están implementados.

## Qué funciona hoy

- Abre un documento con el diálogo, arrastrando un archivo o desde la línea de
  comandos
- Desplazamiento vertical continuo con barra temática, rueda del ratón, teclado y
  arrastre con el botón central
- Selecciona y copia texto: arrastra, doble clic para una palabra, triple clic
  para una línea
- Busca en todo el documento con cada coincidencia resaltada y la activa en un
  color más fuerte; `Ctrl+F` abre la barra, `Enter` y `Shift+Enter` recorren las
  coincidencias
- Una sola ventana por sesión: abrir un segundo PDF lo entrega a la ventana activa
- Zoom de 10% a 800%, ajustar al ancho, volver al 100%
- Rotación en pasos de 90 grados
- Tres temas: Predeterminado, Gris y Claro
- Interfaz en inglés y español
- Recuerda la última página, el zoom y la rotación de cada documento, y los
  archivos recientes
- Detecta documentos protegidos con contraseña y lo informa

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

# Pruebas
dotnet test CyberPDF.slnx -c Release

# Compilación autocontenida en un solo archivo
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
| `Ctrl++` / `Ctrl+-` | Acercar / alejar |
| `Ctrl+0` | Tamaño real |
| `Ctrl+R` | Girar en sentido horario |
| `Ctrl+F` | Buscar en el documento |
| `F3` | Siguiente coincidencia |
| `Ctrl+C` | Copiar la selección |
| `Ctrl+A` | Seleccionar la página actual |
| `Ctrl+W` | Cerrar |
| `F11` | Maximizar / restaurar |
| `Av Pág` / `Re Pág` | Desplazar una pantalla |
| `Inicio` / `Fin` | Principio / final del documento |

## Licencia

GPL-3.0. PDFium y sus dependencias incluidas se distribuyen con sus propias
licencias, que acompañan a la compilación publicada.
