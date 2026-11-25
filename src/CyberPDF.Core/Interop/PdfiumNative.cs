using System.Runtime.InteropServices;

namespace CyberPDF.Core.Interop;

/// <summary>
/// Raw PDFium entry points. Only what CyberPDF uses is declared, all with
/// explicit marshalling and no automatic string conversion on the hot paths.
/// Every handle returned here must be released by the matching close call; the
/// SafeHandle wrappers in <see cref="NativeHandles"/> enforce that.
/// </summary>
internal static partial class PdfiumNative
{
    internal const string Library = "pdfium";

    // Error codes returned by FPDF_GetLastError.
    internal const int ErrorSuccess = 0;
    internal const int ErrorUnknown = 1;
    internal const int ErrorFile = 2;
    internal const int ErrorFormat = 3;
    internal const int ErrorPassword = 4;
    internal const int ErrorSecurity = 5;
    internal const int ErrorPage = 6;

    // FPDF_RenderPageBitmap flag values.
    internal const int RenderAnnotations = 0x01;
    internal const int RenderLcdText = 0x02;
    internal const int RenderNoNativeText = 0x04;
    internal const int RenderGreyscale = 0x08;

    // Bitmap formats accepted by FPDFBitmap_Create.
    internal const int BitmapFormatBgra = 4;
    internal const int BitmapFormatBgr = 2;

    /// <summary>Stops PDFium from reading wall-clock time from documents.</summary>
    internal const int PolicyMachineTimeAccess = 0;

    // --- Library lifetime ---------------------------------------------------

    [LibraryImport(Library)]
    internal static partial void FPDF_InitLibrary();

    [LibraryImport(Library)]
    internal static partial void FPDF_DestroyLibrary();

    [LibraryImport(Library)]
    internal static partial void FPDF_SetSandBoxPolicy(int policy, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [LibraryImport(Library)]
    internal static partial int FPDF_GetLastError();

    // --- Documents ----------------------------------------------------------

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint FPDF_LoadDocument(string path, string? password);

    [LibraryImport(Library)]
    internal static partial nint FPDF_LoadCustomDocument(nint fileAccess, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    [LibraryImport(Library)]
    internal static partial nint FPDF_LoadMemDocument(nint data, int size, [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);

    [LibraryImport(Library)]
    internal static partial void FPDF_CloseDocument(nint document);

    [LibraryImport(Library)]
    internal static partial int FPDF_GetPageCount(nint document);

    [LibraryImport(Library)]
    internal static partial int FPDF_GetPageSizeByIndexF(nint document, int index, out FsSizeF size);

    [LibraryImport(Library)]
    internal static partial uint FPDF_GetDocPermissions(nint document);

    [LibraryImport(Library)]
    internal static partial int FPDF_GetSecurityHandlerRevision(nint document);

    // --- Pages --------------------------------------------------------------

    [LibraryImport(Library)]
    internal static partial nint FPDF_LoadPage(nint document, int index);

    [LibraryImport(Library)]
    internal static partial void FPDF_ClosePage(nint page);

    [LibraryImport(Library)]
    internal static partial float FPDF_GetPageWidthF(nint page);

    [LibraryImport(Library)]
    internal static partial float FPDF_GetPageHeightF(nint page);

    [LibraryImport(Library)]
    internal static partial int FPDFPage_GetRotation(nint page);

    // --- Bitmaps ------------------------------------------------------------

    [LibraryImport(Library)]
    internal static partial nint FPDFBitmap_Create(int width, int height, int alpha);

    /// <summary>
    /// Wraps caller-owned pixel memory as a PDFium bitmap. Used to render
    /// straight into a GDI+ bitmap, so page pixels are never duplicated.
    /// Destroying the wrapper does not free the memory.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial nint FPDFBitmap_CreateEx(int width, int height, int format, nint firstScan, int stride);

    [LibraryImport(Library)]
    internal static partial void FPDFBitmap_Destroy(nint bitmap);

    /// <summary>
    /// Fills a rectangle with a color in 0xAARRGGBB form. A newly created
    /// bitmap is fully transparent, not white: the background has to be painted
    /// before rendering or the page content lands on nothing.
    /// </summary>
    [LibraryImport(Library)]
    internal static partial void FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);

    [LibraryImport(Library)]
    internal static partial nint FPDFBitmap_GetBuffer(nint bitmap);

    [LibraryImport(Library)]
    internal static partial int FPDFBitmap_GetStride(nint bitmap);

    [LibraryImport(Library)]
    internal static partial void FPDF_RenderPageBitmap(
        nint bitmap, nint page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    // --- Text layer ---------------------------------------------------------

    [LibraryImport(Library)]
    internal static partial nint FPDFText_LoadPage(nint page);

    [LibraryImport(Library)]
    internal static partial void FPDFText_ClosePage(nint textPage);

    [LibraryImport(Library)]
    internal static partial int FPDFText_CountChars(nint textPage);

    [LibraryImport(Library)]
    internal static partial uint FPDFText_GetUnicode(nint textPage, int index);

    [LibraryImport(Library)]
    internal static partial int FPDFText_GetCharIndexAtPos(nint textPage, double x, double y, double xTolerance, double yTolerance);

    [LibraryImport(Library)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FPDFText_GetCharBox(
        nint textPage, int index, out double left, out double right, out double bottom, out double top);

    /// <summary>PDFium's own size struct: two consecutive floats.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FsSizeF
    {
        public float Width;
        public float Height;
    }
}
