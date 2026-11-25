using System.Runtime.InteropServices;

namespace CyberPDF.Core.Interop;

/// <summary>
/// Custom file access used to stream a document instead of loading the whole
/// file into memory. Layout matches FPDF_FILEACCESS in fpdfview.h exactly:
/// a 32-bit length, a function pointer and an opaque context pointer.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FpdfFileAccess
{
    /// <summary>Total file length in bytes. PDFium reads at most 4 GB this way.</summary>
    public uint FileLength;

    public nint GetBlock;

    public nint Param;
}

/// <summary>
/// Signature of the FPDF_FILEACCESS callback. Must be a static method pinned
/// for the lifetime of the document: PDFium keeps calling it while the document
/// is open.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int FpdfGetBlockCallback(nint param, uint position, nint buffer, uint size);
