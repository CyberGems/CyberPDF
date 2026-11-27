<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberPDF/main/src/CyberPDF.UI/Assets/CyberPDF.png" width="120" alt="CyberPDF">
</p>

## 📄 CyberPDF {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Keep the Spanish mirror inside the details block in sync: translate the summary and rewrite the highlights. -->
<!-- COVERAGE: list every commit since the previous tag before drafting. Give every user-facing change at least one highlight and group related commits into one. Skip internal refactors, CI, tests and dependency bumps with no visible effect; keep small visible fixes such as copy, tooltips, shortcuts, themes, icons, spacing, stability and perceptible performance. Review the draft if 15+ user-facing commits produce fewer than 6 highlights, or 40+ total commits fewer than 8. Patch releases may have 1-3 highlights or a plain maintenance-only note. -->

<!-- changelog-summary:start -->
Major reading upgrade: multi-document tabs with a Start tab, continuous/single/two-page layouts with dark page mode, floating search pill, clickable PDF links, interactive status bar, and local text highlights plus personal page bookmarks.
<!-- changelog-summary:end -->

> **New to CyberPDF?** A fast, lightweight PDF reader for Windows, built to open in under half a second.

---

### ✨ Key Features & Highlights

- 📑 **Multi-document tabs & Start tab**: open multiple PDFs in one window (`Ctrl+Tab` / `Ctrl+W`), or open a new Welcome tab with recent files via `+` or `Ctrl+T`.
- 🖍️ **Text highlights & personal bookmarks**: highlight text in four colors (`Alt+M` or context menu) and bookmark pages (`Ctrl+B`) stored locally per document in `%LOCALAPPDATA%\CyberPDF\annotations.json`, displayed in the side panel and thumbnail ribbons.
- 📄 **Flexible reading modes**: switch between Continuous, Single Page, and Two Pages, toggle the dark-page reading filter (`Alt+N`), scroll horizontally, or pan with middle-drag / Hand tool (`Alt+H`).
- 🔎 **Floating search pill**: top-right overlay search (`Ctrl+F`) with `Aa` match case, `ab` whole word, zero layout shift, and flicker-free tooltips.
- 🔗 **Clickable PDF links & interactive chrome**: follow internal/external links, inspect Document Properties (`Ctrl+D`), enter passwords in a themed prompt, print (`Ctrl+P`), reload (`F5`), jump pages or zoom from the status bar, and resize the side panel edge.
- 🎨 **Polished icons, menus & live i18n**: crisp vector icons across Default (updated moon icon), Gray, and Light themes, themed context menus on every text input, and live English/Spanish switching in Settings.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Gran mejora de lectura: pestañas para múltiples documentos con pestaña de inicio, modos continuo, página única y dos páginas con modo nocturno de página, píldora flotante de búsqueda, enlaces PDF clicables, barra de estado interactiva y resaltados de texto con marcadores personales locales.

> **¿Nuevo en CyberPDF?** Un lector de PDF rápido y ligero para Windows, que abre en menos de medio segundo.

---

### ✨ Novedades destacadas

- 📑 **Pestañas múltiples y pestaña de inicio**: abre varios PDF en una sola ventana (`Ctrl+Tab` / `Ctrl+W`) o abre una nueva pestaña de bienvenida con recientes usando `+` o `Ctrl+T`.
- 🖍️ **Resaltados de texto y marcadores personales**: resalta texto en cuatro colores (`Alt+M` o menú contextual) y marca páginas (`Ctrl+B`) con guardado local por documento en `%LOCALAPPDATA%\CyberPDF\annotations.json`, visibles en el panel lateral y en las miniaturas.
- 📄 **Modos de lectura flexibles**: alterna entre Continuo, Página única y Dos páginas, activa el filtro nocturno de página (`Alt+N`), desplázate horizontalmente o arrastra con la mano (`Alt+H`).
- 🔎 **Píldora flotante de búsqueda**: buscador flotante arriba a la derecha (`Ctrl+F`) con `Aa`, `ab`, cero saltos de diseño y tooltips sin parpadeos.
- 🔗 **Enlaces clicables e interfaz interactiva**: abre enlaces internos o externos, consulta Propiedades del documento (`Ctrl+D`), desbloquea archivos protegidos en un diálogo temático, imprime (`Ctrl+P`), recarga (`F5`), salta de página o zoom desde la barra de estado y ajusta el ancho del panel lateral.
- 🎨 **Iconos, menús e idioma en vivo**: iconos vectoriales refinados en Predeterminado (luna rediseñada), Gris y Claro, menús contextuales temáticos en cada campo de texto y cambio de idioma español/inglés en vivo desde Ajustes.

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

---

📜 **Full changelog:** browse every version in [CHANGELOG.md](https://github.com/CyberGems/CyberPDF/blob/main/CHANGELOG.md).

*Crafted with precision by [CyberGems](https://cybergems.org)*
