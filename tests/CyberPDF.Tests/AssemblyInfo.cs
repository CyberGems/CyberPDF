using Xunit;

// PDFium is not thread-safe: two threads inside the engine crash with an illegal
// instruction rather than throwing, and results observed from concurrent calls
// are unreliable. Every test class that touches the native engine therefore has
// to run alone in the process. Disabling parallelization for the assembly is the
// only reliable way to guarantee that, and it also mirrors the rule the product
// code follows by serializing all native work through PdfEngine.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CyberPDF.Tests;

/// <summary>
/// Groups tests that touch PDFium. The attribute above already serializes the
/// whole assembly; the collection exists so the intent is explicit at the call
/// site and so a future parallel collection cannot be added by accident next to
/// these classes.
/// </summary>
[CollectionDefinition("pdfium", DisableParallelization = true)]
public sealed class PdfiumCollection
{
}
