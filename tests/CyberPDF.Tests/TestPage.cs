using CyberPDF.Core.Documents;
using CyberPDF.Core.Interop;
using CyberPDF.Core.Rendering;

namespace CyberPDF.Tests;

/// <summary>
/// Creates real rendered pages for cache tests.
/// </summary>
/// <remarks>
/// Rendering needs a valid page handle inside an open document, so this helper
/// builds one small PDF once per process and renders from it. Using the real
/// engine keeps the byte accounting honest: width, height and pixel format come
/// from GDI+ exactly as they do in production.
/// </remarks>
internal static class TestPage
{
    private static readonly Lazy<byte[]> DocumentBytes = new(CreateDocumentBytes);
    private static PdfDocument? _sharedDocument;
    private static readonly object Gate = new();

    public static RenderedPage Create(int width, int height)
    {
        PdfiumLibrary.EnsureInitialized();

        lock (Gate)
        {
            _sharedDocument ??= OpenSharedDocument();

            var pageHandle = PdfiumNative.FPDF_LoadPage(_sharedDocument.Handle, 0);
            if (pageHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not load the shared test page.");
            }

            try
            {
                return RenderedPage.Render(
                    pageHandle,
                    width,
                    height,
                    rotation: 0,
                    tileOffsetY: 0,
                    fullPixelHeight: height);
            }
            finally
            {
                PdfiumNative.FPDF_ClosePage(pageHandle);
            }
        }
    }

    private static PdfDocument OpenSharedDocument()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cyberpdf-testpage");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "blank.pdf");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, DocumentBytes.Value);
        }

        var result = PdfDocument.Open(path);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Could not open the shared test document: {result.Error}");
        }
        return result.Document!;
    }

    /// <summary>Writes one A4 page with a single text line, used only as a render source.</summary>
    private static byte[] CreateDocumentBytes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cyberpdf-testpage");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "source-" + Guid.NewGuid().ToString("N") + ".pdf");
        PdfWriter.Write(path, pages: 1, linesPerPage: 1);
        var bytes = File.ReadAllBytes(path);
        File.Delete(path);
        return bytes;
    }
}
