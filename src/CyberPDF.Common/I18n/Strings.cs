namespace CyberPDF.Common.I18n;

/// <summary>
/// Single source of truth for user-facing text. Every entry carries both
/// languages so a missing translation is a compile-time problem, not a runtime
/// surprise: the tuple is required by the dictionary.
/// </summary>
public static class Strings
{
    public static Lang Current { get; set; } = Lang.Es;

    private static readonly Dictionary<string, (string En, string Es)> Map = new()
    {
        // Window and app chrome
        ["AppTitle"] = ("CyberPDF", "CyberPDF"),
        ["AppSubtitle"] = ("Fast PDF reader", "Lector de PDF rápido"),

        // Commands
        ["Open"] = ("Open", "Abrir"),
        ["OpenFile"] = ("Open PDF...", "Abrir PDF..."),
        ["CloseDocument"] = ("Close document", "Cerrar documento"),
        ["ReloadDocument"] = ("Reload", "Recargar"),
        ["ZoomIn"] = ("Zoom in", "Acercar"),
        ["ZoomOut"] = ("Zoom out", "Alejar"),
        ["ZoomReset"] = ("Actual size", "Tamaño real"),
        ["FitWidth"] = ("Fit width", "Ajustar ancho"),
        ["FitPage"] = ("Fit page", "Ajustar página"),
        ["RotateLeft"] = ("Rotate left", "Girar a la izquierda"),
        ["RotateRight"] = ("Rotate right", "Girar a la derecha"),
        ["Settings"] = ("Settings", "Ajustes"),
        ["ChangeTheme"] = ("Change theme", "Cambiar tema"),
        ["Thumbnails"] = ("Pages", "Páginas"),
        ["Bookmarks"] = ("Bookmarks", "Marcadores"),
        ["NoBookmarks"] = ("This document has no bookmarks", "Este documento no tiene marcadores"),
        ["PageNumber"] = ("Page {0}", "Página {0}"),
        ["ToggleSidePanel"] = ("Pages and bookmarks", "Páginas y marcadores"),
        ["Minimize"] = ("Minimize", "Minimizar"),
        ["Maximize"] = ("Maximize", "Maximizar"),
        ["Restore"] = ("Restore", "Restaurar"),
        ["CloseWindow"] = ("Close window", "Cerrar ventana"),

        // Status bar
        ["PageOf"] = ("Page {0} of {1}", "Página {0} de {1}"),
        ["ZoomPercent"] = ("{0}%", "{0}%"),
        ["NoDocument"] = ("No document open", "Ningún documento abierto"),
        ["Ready"] = ("Ready", "Listo"),
        ["Loading"] = ("Loading...", "Cargando..."),
        ["Rendering"] = ("Rendering page {0}...", "Generando página {0}..."),

        // Empty state
        ["DropHint"] = ("Drop a PDF here or press Ctrl+O", "Arrastra un PDF aquí o pulsa Ctrl+O"),
        ["OpenRecent"] = ("Recent documents", "Documentos recientes"),
        ["NoRecent"] = ("No recent documents yet", "Todavía no hay documentos recientes"),
        ["RemoveFromList"] = ("Remove from list", "Quitar de la lista"),
        ["ClearRecent"] = ("Clear list", "Limpiar lista"),
        ["GoToPage"] = ("Go to page", "Ir a la página"),
        ["PreviousPage"] = ("Previous page", "Página anterior"),
        ["NextPage"] = ("Next page", "Página siguiente"),
        ["ZoomLevel"] = ("Zoom level", "Nivel de zoom"),
        ["HandTool"] = ("Hand tool (pan)", "Herramienta mano (desplazar)"),
        ["SelectTool"] = ("Select text", "Seleccionar texto"),

        // Errors
        ["ErrorTitle"] = ("Cannot open the document", "No se puede abrir el documento"),
        ["ErrorNotFound"] = ("The file no longer exists.", "El archivo ya no existe."),
        ["ErrorAccess"] = ("Windows denied access to the file.", "Windows denegó el acceso al archivo."),
        ["ErrorCorrupt"] = ("The file is not a valid PDF or it is damaged.", "El archivo no es un PDF válido o está dañado."),
        ["ErrorPassword"] = ("This document is password protected.", "Este documento está protegido con contraseña."),
        ["ErrorUnsupported"] = ("This document uses features this reader does not support yet.", "Este documento usa funciones que este lector aún no admite."),
        ["ErrorUnknown"] = ("The document could not be opened (code {0}).", "No se pudo abrir el documento (código {0})."),
        ["PasswordRequired"] = ("Password required", "Contraseña necesaria"),
        ["PasswordPrompt"] = ("Enter the password for this document.", "Escribe la contraseña de este documento."),
        ["Password"] = ("Password", "Contraseña"),
        ["WrongPassword"] = ("That password is not correct.", "La contraseña no es correcta."),

        // About
        ["About"] = ("About CyberPDF", "Acerca de CyberPDF"),
        ["Engine"] = ("PDF engine", "Motor PDF"),

        // Search
        ["Search"] = ("Search", "Buscar"),
        ["SearchPlaceholder"] = ("Search in document...", "Buscar en el documento..."),
        ["SearchNext"] = ("Next match", "Siguiente coincidencia"),
        ["SearchPrevious"] = ("Previous match", "Coincidencia anterior"),
        ["SearchNoResults"] = ("No results", "Sin resultados"),
        ["SearchResults"] = ("{0} of {1}", "{0} de {1}"),
        ["SearchNoDocument"] = ("Open a document to search", "Abre un documento para buscar"),
        ["SearchClear"] = ("Clear search", "Borrar búsqueda"),
        ["MatchCase"] = ("Match case", "Distinguir mayúsculas"),
        ["WholeWord"] = ("Whole word", "Palabra completa"),

        // Text selection
        ["Copy"] = ("Copy", "Copiar"),
        ["Cut"] = ("Cut", "Cortar"),
        ["Paste"] = ("Paste", "Pegar"),
        ["Undo"] = ("Undo", "Deshacer"),
        ["Redo"] = ("Redo", "Rehacer"),
        ["Delete"] = ("Delete", "Eliminar"),
        ["SelectAll"] = ("Select all", "Seleccionar todo"),
        ["NothingSelected"] = ("Select some text first.", "Selecciona texto primero."),
        ["CopiedCharacters"] = ("Copied {0} characters.", "Se copiaron {0} caracteres."),
        ["SelectTextHint"] = ("Drag to select text. Middle-drag to scroll.", "Arrastra para seleccionar texto. Arrastra con el botón central para desplazarte."),

        // Common dialog buttons
        ["Cancel"] = ("Cancel", "Cancelar"),
        ["Accept"] = ("Accept", "Aceptar"),
    };

    private static readonly Dictionary<string, (string En, string Es)> MapFallback = Map;

    public static string Get(string key)
    {
        if (!MapFallback.TryGetValue(key, out var pair))
        {
            // A missing key is a programming error; surface the key instead of an
            // empty string so it is visible during development and in reports.
            return "{" + key + "}";
        }
        return Current == Lang.Es ? pair.Es : pair.En;
    }

    public static string Format(string key, params object[] args)
        => string.Format(System.Globalization.CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Every key defined, for completeness tests.</summary>
    public static IEnumerable<string> Keys => Map.Keys;
}
