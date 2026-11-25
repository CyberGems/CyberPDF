using CyberPDF.Core.Documents;
using Xunit;

namespace CyberPDF.Tests;

/// <summary>
/// Opening a document calls into PDFium, so these run on the same serialized
/// path the app uses. <c>AssemblyInfo.cs</c> disables parallelization for the
/// assembly, which covers them.
/// </summary>
public sealed class BookmarkReaderTests
{
    [Fact]
    public void ReadsNestedOutlineInOrderWithDepth()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cyberpdf-outline-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfWriter.WriteWithOutline(path, 4);
            var result = PdfDocument.Open(path);
            using var document = result.Document!;

            var bookmarks = BookmarkReader.Read(document);

            Assert.Equal(3, bookmarks.Count);
            Assert.Equal("Chapter 1", bookmarks[0].Title);
            Assert.Equal(0, bookmarks[0].PageIndex);
            Assert.Equal(0, bookmarks[0].Depth);

            Assert.Equal("Section 1.1", bookmarks[1].Title);
            Assert.Equal(1, bookmarks[1].PageIndex);
            Assert.Equal(1, bookmarks[1].Depth);

            Assert.Equal("Chapter 2", bookmarks[2].Title);
            Assert.Equal(3, bookmarks[2].PageIndex);
            Assert.Equal(0, bookmarks[2].Depth);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DocumentWithoutOutlineReturnsNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cyberpdf-plain-{Guid.NewGuid():N}.pdf");
        try
        {
            PdfWriter.Write(path, 2);
            var result = PdfDocument.Open(path);
            using var document = result.Document!;

            Assert.Empty(BookmarkReader.Read(document));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptyTitleIsKeptRatherThanDropped()
    {
        // A bookmark with a destination but no title still navigates, so the row
        // exists; the panel shows an indent with nothing in it, which is honest
        // about the document instead of hiding the entry.
        var entry = new BookmarkEntry(string.Empty, 2, 1);
        Assert.Equal(string.Empty, entry.Title);
        Assert.Equal(2, entry.PageIndex);
        Assert.Equal(1, entry.Depth);
    }
}
