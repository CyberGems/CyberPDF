namespace CyberPDF.Core.Documents;

/// <summary>Why a document could not be opened, mapped to a localized message by the UI.</summary>
public enum PdfOpenError
{
    None,
    NotFound,
    AccessDenied,
    InvalidFormat,
    PasswordRequired,
    WrongPassword,
    Security,
    Unsupported,
    Unknown,
}

/// <summary>Result of an open attempt: either a document or a reason.</summary>
public sealed class PdfOpenResult
{
    private PdfOpenResult(PdfDocument? document, PdfOpenError error, int pdfiumError)
    {
        Document = document;
        Error = error;
        PdfiumError = pdfiumError;
    }

    public PdfDocument? Document { get; }

    public PdfOpenError Error { get; }

    /// <summary>Raw FPDF_GetLastError code, for logs and diagnostics.</summary>
    public int PdfiumError { get; }

    public bool Success => Document is not null;

    internal static PdfOpenResult Ok(PdfDocument document) => new(document, PdfOpenError.None, 0);

    internal static PdfOpenResult Fail(PdfOpenError error, int pdfiumError = 0) => new(null, error, pdfiumError);

    /// <summary>Wraps an unexpected failure so callers always get a result.</summary>
    public static PdfOpenResult FromException(Exception exception) => exception switch
    {
        DllNotFoundException => new PdfOpenResult(null, PdfOpenError.Unsupported, 0),
        OutOfMemoryException => new PdfOpenResult(null, PdfOpenError.Unknown, 0),
        UnauthorizedAccessException => new PdfOpenResult(null, PdfOpenError.AccessDenied, 0),
        FileNotFoundException or DirectoryNotFoundException => new PdfOpenResult(null, PdfOpenError.NotFound, 0),
        _ => new PdfOpenResult(null, PdfOpenError.Unknown, 0),
    };
}
