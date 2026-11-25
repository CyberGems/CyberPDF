using Microsoft.Win32.SafeHandles;

namespace CyberPDF.Core.Interop;

/// <summary>Owns an FPDF_DOCUMENT handle.</summary>
internal sealed class SafePdfDocument : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafePdfDocument(nint handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDF_CloseDocument(handle);
        return true;
    }
}

/// <summary>Owns an FPDF_PAGE handle.</summary>
internal sealed class SafePdfPage : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafePdfPage(nint handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDF_ClosePage(handle);
        return true;
    }
}

/// <summary>Owns an FPDF_BITMAP handle.</summary>
internal sealed class SafePdfBitmap : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafePdfBitmap(nint handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDFBitmap_Destroy(handle);
        return true;
    }
}

/// <summary>Owns an FPDF_TEXTPAGE handle.</summary>
internal sealed class SafePdfTextPage : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafePdfTextPage(nint handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        PdfiumNative.FPDFText_ClosePage(handle);
        return true;
    }
}
