<p align="center">
  <img src="https://raw.githubusercontent.com/CyberGems/CyberPDF/main/src/CyberPDF.UI/Assets/CyberPDF.png" width="120" alt="CyberPDF">
</p>

## 📄 CyberPDF {{VERSION}}: Release Notes

### 🚀 What's new in this release

<!-- IMPORTANT FOR AGENTS & MAINTAINERS: Always rewrite this entire section and highlights with the ACTUAL release notes for this specific new version before tagging. NEVER leave or copy previous version notes! Use 25-45 words for the summary, lead with user-facing changes, and do not repeat the app name or version. Keep the Spanish mirror inside the details block in sync: translate the summary and rewrite the highlights. -->
<!-- COVERAGE: list every commit since the previous tag before drafting. Give every user-facing change at least one highlight and group related commits into one. Skip internal refactors, CI, tests and dependency bumps with no visible effect; keep small visible fixes such as copy, tooltips, shortcuts, themes, icons, spacing, stability and perceptible performance. Review the draft if 15+ user-facing commits produce fewer than 6 highlights, or 40+ total commits fewer than 8. Patch releases may have 1-3 highlights or a plain maintenance-only note. -->

<!-- changelog-summary:start -->
Desktop polish release: system tray icon and quick menu, themed About dialog with GitHub update check, official CyberPDF branding icon, default PDF reader setup, inline bookmark renaming, and remembered highlight colors.
<!-- changelog-summary:end -->

> **New to CyberPDF?** A fast, lightweight PDF reader for Windows, built to open in under half a second.

---

### ✨ Key Features & Highlights

- 🧭 **System tray icon & quick menu**: toggle window visibility, open a PDF, switch to recent documents, open Settings, or reach Help and update checks straight from the notification area.
- 💎 **Themed About dialog & update check**: inspect the version, check GitHub Releases for updates, copy diagnostics in one click, and discover sibling apps in the CyberGems suite.
- 🎨 **Official CyberPDF icon across the app**: sharp new icon artwork in the titlebar, welcome view, taskbar, system tray, About modal, and Windows installer.
- 📄 **Default PDF reader setup**: register as the `.pdf` handler per user from Settings or keep the default-checked option in the installer, with direct handoff to Windows Default Apps when needed.
- 🖍️ **Inline bookmark renaming & remembered highlight color**: double-click or right-click any personal bookmark to rename it in place, and pick a persistent default highlighter color from the toolbar swatch.

<details>
<summary><b>🇪🇸 Ver notas de la versión en Español</b></summary>

### 🚀 Novedades de esta versión

Mejoras de escritorio y acabado: icono y menú rápido en la bandeja del sistema, diálogo Acerca de con búsqueda de actualizaciones en GitHub, icono oficial de CyberPDF, configuración como lector predeterminado, renombrado de marcadores y selector de color de resaltado.

> **¿Nuevo en CyberPDF?** Un lector de PDF rápido y ligero para Windows, que abre en menos de medio segundo.

---

### ✨ Novedades destacadas

- 🧭 **Icono y menú rápido en la bandeja del sistema**: muestra u oculta la ventana, abre un PDF, accede a documentos recientes o abre Configuración, Ayuda y actualizaciones desde el área de notificaciones.
- 💎 **Diálogo Acerca de temático y comprobación de actualizaciones**: consulta la versión, busca nuevas versiones en GitHub Releases, copia información de diagnóstico en un clic y explora otras apps de CyberGems.
- 🎨 **Icono oficial de CyberPDF en toda la app**: nuevo icono en barra de título, bienvenida, barra de tareas, bandeja del sistema, diálogo Acerca de e instalador.
- 📄 **Configuración como lector de PDF predeterminado**: regístrate como lector de `.pdf` por usuario desde Ajustes o con la opción marcada por defecto en el instalador, abriendo Aplicaciones predeterminadas cuando Windows lo requiera.
- 🖍️ **Renombrado de marcadores y color de resaltado recordado**: haz doble clic o clic derecho para renombrar tus marcadores personales en línea y elige un color predeterminado para el resaltador desde la barra.

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
