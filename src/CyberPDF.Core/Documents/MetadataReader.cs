using System.Runtime.InteropServices;
using CyberPDF.Core.Interop;

namespace CyberPDF.Core.Documents;

/// <summary>Standard PDF metadata fields read on the PDFium thread.</summary>
public sealed record PdfMetadata(
    string? Title,
    string? Author,
    string? Subject,
    string? Creator,
    string? Producer);

public static class MetadataReader
{
    public static PdfMetadata Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.IsDisposed)
        {
            return new PdfMetadata(null, null, null, null, null);
        }

        return new PdfMetadata(
            Title: ReadTag(document.Handle, "Title"),
            Author: ReadTag(document.Handle, "Author"),
            Subject: ReadTag(document.Handle, "Subject"),
            Creator: ReadTag(document.Handle, "Creator"),
            Producer: ReadTag(document.Handle, "Producer"));
    }

    private static string? ReadTag(nint docHandle, string tag)
    {
        var length = PdfiumNative.FPDF_GetMetaText(docHandle, tag, nint.Zero, 0);
        if (length <= 2 || length > 65536)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            var written = PdfiumNative.FPDF_GetMetaText(docHandle, tag, buffer, length);
            if (written <= 2)
            {
                return null;
            }
            var chars = (int)(Math.Min(written, length) / sizeof(char));
            var value = Marshal.PtrToStringUni(buffer, chars)?.TrimEnd('\0').Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
