<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberPDF/main/src/CyberPDF.UI/Assets/CyberPDF.png" width="120" alt="CyberPDF">
</p>

## 📄 CyberPDF {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- changelog-summary:start -->
Stability and workflow release fixing the tall-page rendering crash, repairing text selection with multi-page drag and auto-scroll, setting Hand as the default mode beside Select Text, and adding Save As, the close-to-tray prompt, centered titlebar filenames, and disabled-button tooltips.
<!-- changelog-summary:end -->

> **New to CyberPDF?** A fast, lightweight PDF reader for Windows, built to open in under half a second.

---

### ✨ Key Features & Highlights

- 🖍️ **Fixed multi-page text selection & Hand/Select tools**: repaired cached-rectangle and async extraction bugs, kept line-end matching accurate, added edge auto-scroll, extended selection, copying, and highlights across pages, and added side-by-side **Hand** (`Alt+H`, default with clickable links) and **Select text** (`Alt+S`) buttons.
- 💾 **Save As & desktop window polish**: press `Ctrl+S` or click Save beside Print (plus page and Settings menus) to save a copy with local annotations, choose tray or quit from the new close confirmation dialog, see centered filenames and clickable branding in the titlebar (`Win+Down`/`Win+Up`/`Alt+F4`), and read tooltips on disabled controls.
- 🛡️ **Tall multiband PDF stability fix**: reference-counted page bands across the render cache and viewer prevent `Parameter is not valid` crashes when scrolling or zooming oversized documents.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Versión de estabilidad y flujo de trabajo que corrige el fallo en páginas altas, repara la selección con arrastre multipágina y autodesplazamiento, deja Mano por defecto junto a Selección de texto y añade Guardar como, modal de cierre, título centrado y tooltips en controles deshabilitados.

> **¿Nuevo en CyberPDF?** Un lector de PDF rápido y ligero para Windows, que abre en menos de medio segundo.

---

### ✨ Novedades destacadas

- 🖍️ **Selección de texto multipágina y botones Mano / Selección**: corregidos el caché y la carrera asíncrona al arrastrar, mejorada la precisión por línea, añadido autodesplazamiento y soporte multipágina al copiar o resaltar, junto a los botones **Mano** (`Alt+H`, predeterminado con enlaces clicables) y **Selección de texto** (`Alt+S`).
- 💾 **Guardar como... y mejoras de ventana**: pulsa `Ctrl+S` o usa el botón Guardar (también en los menús contextual y de Ajustes) para guardar una copia con sus anotaciones, elige bandeja o salir en el nuevo diálogo al cerrar la ventana, mira el archivo centrado y branding clicable en el título (`Win+↓`/`Win+↑`/`Alt+F4`), y consulta tooltips incluso en botones deshabilitados.
- 🛡️ **Estabilidad en páginas altas multibanda**: el conteo de referencias sobre franjas renderizadas evita cierres inesperados (`Parameter is not valid`) al desplazar o cambiar el zoom en documentos grandes.

### ℹ️ Antes de instalar

- **Windows:** esta versión aún no está firmada digitalmente. SmartScreen puede avisar antes de ejecutarla: **Más información › Ejecutar de todas formas**. Cuando Smart App Control está activado, Windows rechaza programas sin firmar; consulta la [política de firma de código](https://github.com/CyberGems/.github/blob/main/CODE_SIGNING.md). Verifica la descarga con los checksums SHA256 que aparecen más abajo.

</details>

---

### ℹ️ Before you install

- **Windows:** this release is not code-signed yet. SmartScreen may ask before it runs: **More info › Run anyway**. Where Smart App Control is on, Windows refuses unsigned programs; see the [code signing policy](https://github.com/CyberGems/.github/blob/main/CODE_SIGNING.md). Verify the download with the SHA256 checksums below.

---

### 📦 Downloads & Packages

> ⬇️ **Direct download:** click a file name to download it now. Same files as in **Assets** at the bottom of this page.

| File | Description | Platform |
| :--- | :--- | :--- |
| **[`CyberPDF-Setup-{{VERSION_BARE}}.exe`](https://github.com/CyberGems/CyberPDF/releases/download/{{VERSION}}/CyberPDF-Setup-{{VERSION_BARE}}.exe)** | 🚀 **Recommended Installer** (Inno Setup, optional `.pdf` association, no administrator rights) | Windows 10 / 11 (x64) |
| **[`CyberPDF-{{VERSION}}-Portable-win-x64.zip`](https://github.com/CyberGems/CyberPDF/releases/download/{{VERSION}}/CyberPDF-{{VERSION}}-Portable-win-x64.zip)** | 💼 **Portable package** (unzip into any folder and run `CyberPDF.exe`) | Windows 10 / 11 (x64) |

---

### 🔐 VirusTotal and SHA256

> 🛡️ The report opens on VirusTotal and does not download anything. Sometimes one or two antivirus providers flag a safe file while the rest clear it; that is usually a false alarm. Our software is threat-free and the source code is public, so anyone can check it. Compare the SHA-256 below with your download and use only the links on this page.
> 🛡️ El informe se abre en VirusTotal y no descarga nada. A veces uno o dos proveedores de antivirus marcan un archivo seguro mientras los demás lo dan por limpio; suele ser una falsa alarma. Nuestro software está libre de amenazas y el código es público, cualquiera puede comprobarlo. Compara el SHA-256 de abajo con tu descarga y usa solo los enlaces de esta página.

**Installer**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{INSTALLER_HASH}})

```text
{{INSTALLER_HASH}}
```

**Portable**: [View VirusTotal report](https://www.virustotal.com/gui/file/{{PORTABLE_HASH}})

```text
{{PORTABLE_HASH}}
```

The checksums are also available as a downloadable [SHA256SUMS.txt](https://github.com/CyberGems/CyberPDF/releases/download/{{VERSION}}/SHA256SUMS.txt).

---

📜 **Full changelog:** browse every version in [CHANGELOG.md](https://github.com/CyberGems/CyberPDF/blob/main/CHANGELOG.md).

*Crafted with precision by [CyberGems](https://cybergems.org)*
