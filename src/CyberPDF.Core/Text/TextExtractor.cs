using CyberPDF.Core.Documents;
using CyberPDF.Core.Interop;

namespace CyberPDF.Core.Text;

/// <summary>
/// Extracts the text layer of a page. Must run on the render thread: it touches
/// the same PDFium document handle as rendering.
/// </summary>
public static class TextExtractor
{
    /// <summary>
    /// Builds the text layer for one page. A page without extractable text (a
    /// scan, for example) returns an empty layer, never null.
    /// </summary>
    public static PageTextLayer Extract(PdfDocument document, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);

        if ((uint)pageIndex >= (uint)document.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        var size = document.GetPageSize(pageIndex);
        var pageHandle = PdfiumNative.FPDF_LoadPage(document.Handle, pageIndex);
        if (pageHandle == IntPtr.Zero)
        {
            return PageTextLayer.Empty(size);
        }

        using var page = new SafePdfPage(pageHandle);

        var textHandle = PdfiumNative.FPDFText_LoadPage(page.DangerousGetHandle());
        if (textHandle == IntPtr.Zero)
        {
            return PageTextLayer.Empty(size);
        }

        using var textPage = new SafePdfTextPage(textHandle);
        var count = PdfiumNative.FPDFText_CountChars(textPage.DangerousGetHandle());
        if (count <= 0)
        {
            return PageTextLayer.Empty(size);
        }

        var characters = new TextCharacter[count];
        var handle = textPage.DangerousGetHandle();
        for (var i = 0; i < count; i++)
        {
            var unicode = PdfiumNative.FPDFText_GetUnicode(handle, i);
            if (!PdfiumNative.FPDFText_GetCharBox(handle, i, out var left, out var right, out var bottom, out var top))
            {
                // A character without a box still belongs in the text stream, so
                // it can be selected as part of a range; it just has no area.
                characters[i] = new TextCharacter(i, (int)unicode, 0, 0, 0, 0);
                continue;
            }

            characters[i] = new TextCharacter(i, (int)unicode, left, right, bottom, top);
        }

        return PageTextLayer.Create(characters, size);
    }
}
