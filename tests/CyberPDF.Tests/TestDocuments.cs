using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;

namespace CyberPDF.Tests;

/// <summary>Builds layouts over a synthetic page table, without PDFium.</summary>
internal static class TestDocuments
{
    public static DocumentLayout Layout((float Width, float Height)[] sizes, float zoom, int gap, int padding)
    {
        var pageSizes = sizes.Select(s => new PageSize(s.Width, s.Height)).ToArray();
        return new DocumentLayout(pageSizes, zoom, gap, padding);
    }
}
