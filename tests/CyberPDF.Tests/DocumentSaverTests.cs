using System.Text;
using CyberPDF.Core.Documents;
using Xunit;

namespace CyberPDF.Tests;

public sealed class DocumentSaverTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CyberPDF.SaveTests." + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void SaveCopyCopiesActivelyOpenSharedFile()
    {
        Directory.CreateDirectory(_dir);
        var source = Path.Combine(_dir, "source.pdf");
        var destination = Path.Combine(_dir, "sub", "copy.pdf");
        var payload = Encoding.ASCII.GetBytes("%PDF-1.4\n%sample payload\n%%EOF\n");
        File.WriteAllBytes(source, payload);

        using var openReader = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var copied = DocumentSaver.SaveCopy(source, destination);

        Assert.True(copied);
        Assert.Equal(payload, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(destination)!, "*.tmp.*"));
    }

    [Fact]
    public void SaveCopyAtomicallyOverwritesExistingDestination()
    {
        Directory.CreateDirectory(_dir);
        var source = Path.Combine(_dir, "source.pdf");
        var destination = Path.Combine(_dir, "existing.pdf");
        var fresh = Encoding.ASCII.GetBytes("%PDF-1.4\nnew content\n%%EOF\n");
        File.WriteAllBytes(source, fresh);
        File.WriteAllBytes(destination, Encoding.ASCII.GetBytes("old stale bytes"));

        var copied = DocumentSaver.SaveCopy(source, destination);

        Assert.True(copied);
        Assert.Equal(fresh, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp.*"));
    }

    [Fact]
    public void SaveCopyReturnsFalseWhenSourceEqualsDestination()
    {
        Directory.CreateDirectory(_dir);
        var source = Path.Combine(_dir, "same.pdf");
        var payload = Encoding.ASCII.GetBytes("%PDF-1.4\nuntouched\n%%EOF\n");
        File.WriteAllBytes(source, payload);

        var copied = DocumentSaver.SaveCopy(source, source);

        Assert.False(copied);
        Assert.Equal(payload, File.ReadAllBytes(source));
    }

    [Fact]
    public void SaveCopyThrowsWhenSourceMissing()
    {
        Directory.CreateDirectory(_dir);
        var missing = Path.Combine(_dir, "missing.pdf");
        var destination = Path.Combine(_dir, "out.pdf");

        Assert.Throws<FileNotFoundException>(() => DocumentSaver.SaveCopy(missing, destination));
    }
}
