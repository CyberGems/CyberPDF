using System.Runtime.InteropServices;
using CyberPDF.Core.Interop;
using CyberPDF.Core.Text;

namespace CyberPDF.Core.Documents;

/// <summary>A clickable link annotation inside a PDF page.</summary>
public sealed record PdfLink(PdfRect Bounds, int? TargetPage, string? Uri);

/// <summary>Links extracted for a single page, hit-tested in PDF space.</summary>
public sealed class PageLinkLayer
{
    public static readonly PageLinkLayer Empty = new(Array.Empty<PdfLink>());

    public PageLinkLayer(IReadOnlyList<PdfLink> links)
    {
        Links = links;
    }

    public IReadOnlyList<PdfLink> Links { get; }

    public PdfLink? HitTest(double pdfX, double pdfY)
    {
        for (var i = 0; i < Links.Count; i++)
        {
            var b = Links[i].Bounds;
            if (pdfX >= b.Left && pdfX <= b.Right && pdfY >= b.Bottom && pdfY <= b.Top)
            {
                return Links[i];
            }
        }
        return null;
    }
}

/// <summary>
/// Reads link annotations (internal GoTo destinations and safe external URIs)
/// on the PDFium worker thread.
/// </summary>
public static class LinkReader
{
    public static PageLinkLayer Read(PdfDocument document, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.IsDisposed || pageIndex < 0 || pageIndex >= document.PageCount)
        {
            return PageLinkLayer.Empty;
        }

        var pageHandle = PdfiumNative.FPDF_LoadPage(document.Handle, pageIndex);
        if (pageHandle == IntPtr.Zero)
        {
            return PageLinkLayer.Empty;
        }

        using var page = new SafePdfPage(pageHandle);
        var links = new List<PdfLink>();
        var pos = 0;

        while (PdfiumNative.FPDFLink_Enumerate(pageHandle, ref pos, out var linkAnnot) != 0 && linkAnnot != IntPtr.Zero)
        {
            if (PdfiumNative.FPDFLink_GetAnnotRect(linkAnnot, out var rect) == 0)
            {
                continue;
            }

            var bounds = PdfRect.FromEdges(rect.Left, rect.Bottom, rect.Right, rect.Top);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                continue;
            }

            int? targetPage = null;
            string? uri = null;

            var dest = PdfiumNative.FPDFLink_GetDest(document.Handle, linkAnnot);
            if (dest != IntPtr.Zero)
            {
                var idx = PdfiumNative.FPDFDest_GetDestPageIndex(document.Handle, dest);
                if (idx >= 0 && idx < document.PageCount)
                {
                    targetPage = idx;
                }
            }
            else
            {
                var action = PdfiumNative.FPDFLink_GetAction(linkAnnot);
                if (action != IntPtr.Zero)
                {
                    var type = PdfiumNative.FPDFAction_GetType(action);
                    if (type == PdfiumNative.ActionGoTo)
                    {
                        var actionDest = PdfiumNative.FPDFAction_GetDest(document.Handle, action);
                        if (actionDest != IntPtr.Zero)
                        {
                            var idx = PdfiumNative.FPDFDest_GetDestPageIndex(document.Handle, actionDest);
                            if (idx >= 0 && idx < document.PageCount)
                            {
                                targetPage = idx;
                            }
                        }
                    }
                    else if (type == PdfiumNative.ActionUri)
                    {
                        var rawUri = ReadUri(document.Handle, action);
                        if (IsSafeExternalUri(rawUri))
                        {
                            uri = rawUri;
                        }
                    }
                }
            }

            if (targetPage is not null || uri is not null)
            {
                links.Add(new PdfLink(bounds, targetPage, uri));
            }
        }

        return links.Count == 0 ? PageLinkLayer.Empty : new PageLinkLayer(links);
    }

    public static bool IsSafeExternalUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }
        if (!Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }
        return parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || parsed.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadUri(nint docHandle, nint action)
    {
        var length = PdfiumNative.FPDFAction_GetURIPath(docHandle, action, nint.Zero, 0);
        if (length == 0 || length > 8192)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            var written = PdfiumNative.FPDFAction_GetURIPath(docHandle, action, buffer, length);
            if (written == 0)
            {
                return null;
            }
            var raw = Marshal.PtrToStringUTF8(buffer, (int)Math.Min(written, length));
            return raw?.TrimEnd('\0').Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
