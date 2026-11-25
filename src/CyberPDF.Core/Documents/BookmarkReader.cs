namespace CyberPDF.Core.Documents;

/// <summary>
/// One entry of the document outline, flattened while keeping its depth.
/// </summary>
/// <remarks>
/// A tree would be the obvious model, but the only consumer is a vertical list
/// where depth is drawn as indentation. Flattening once in Core keeps the UI
/// free of recursion and makes the ordering trivial to test.
/// </remarks>
public sealed record BookmarkEntry(string Title, int PageIndex, int Depth);

/// <summary>
/// Reads the document outline.
/// </summary>
/// <remarks>
/// Runs on the PDFium thread like every other native call. A bookmark points at
/// a destination, sometimes wrapped in an action, and either may be missing or
/// name a page out of range, so entries that cannot be resolved are skipped
/// instead of producing a row that does nothing when clicked.
/// </remarks>
public static class BookmarkReader
{
    /// <summary>Guards against a malformed document with a cyclic outline.</summary>
    private const int MaxEntries = 5000;

    private const int MaxDepth = 32;

    public static IReadOnlyList<BookmarkEntry> Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var result = new List<BookmarkEntry>();
        if (document.IsDisposed)
        {
            return result;
        }

        var first = Interop.PdfiumNative.FPDFBookmark_GetFirstChild(document.Handle, nint.Zero);
        if (first == nint.Zero)
        {
            return result;
        }

        Walk(document, first, 0, result, new HashSet<nint>());
        return result;
    }

    private static void Walk(
        PdfDocument document,
        nint bookmark,
        int depth,
        List<BookmarkEntry> result,
        HashSet<nint> visited)
    {
        var node = bookmark;
        while (node != nint.Zero && result.Count < MaxEntries)
        {
            // A cycle would otherwise loop until the entry cap; tracking the
            // handles already walked stops it at the repeated node.
            if (!visited.Add(node))
            {
                return;
            }

            var pageIndex = ResolvePage(document, node);
            var title = ReadTitle(node);
            if (pageIndex >= 0 && pageIndex < document.PageCount)
            {
                result.Add(new BookmarkEntry(title, pageIndex, depth));
            }

            if (depth < MaxDepth)
            {
                var child = Interop.PdfiumNative.FPDFBookmark_GetFirstChild(document.Handle, node);
                if (child != nint.Zero)
                {
                    Walk(document, child, depth + 1, result, visited);
                }
            }

            node = Interop.PdfiumNative.FPDFBookmark_GetNextSibling(document.Handle, node);
        }
    }

    private static int ResolvePage(PdfDocument document, nint bookmark)
    {
        var dest = Interop.PdfiumNative.FPDFBookmark_GetDest(document.Handle, bookmark);
        if (dest == nint.Zero)
        {
            // Bookmarks created from a link carry the destination in an action.
            var action = Interop.PdfiumNative.FPDFBookmark_GetAction(bookmark);
            if (action != nint.Zero)
            {
                dest = Interop.PdfiumNative.FPDFAction_GetDest(document.Handle, action);
            }
        }

        return dest == nint.Zero
            ? -1
            : Interop.PdfiumNative.FPDFDest_GetDestPageIndex(document.Handle, dest);
    }

    private static string ReadTitle(nint bookmark)
    {
        var length = Interop.PdfiumNative.FPDFBookmark_GetTitle(bookmark, nint.Zero, 0);
        if (length == 0)
        {
            return string.Empty;
        }

        // The length is in bytes and includes a UTF-16 terminator.
        var chars = (int)(length / sizeof(char));
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)length);
        try
        {
            Interop.PdfiumNative.FPDFBookmark_GetTitle(bookmark, buffer, length);
            var text = System.Runtime.InteropServices.Marshal.PtrToStringUni(buffer, chars) ?? string.Empty;
            return text.TrimEnd('\0').Trim();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }
}
