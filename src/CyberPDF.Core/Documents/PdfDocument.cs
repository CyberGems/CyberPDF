using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using CyberPDF.Core.Interop;

namespace CyberPDF.Core.Documents;

/// <summary>
/// An open PDF document. Owns the native document handle plus the file stream
/// used for streaming reads.
/// </summary>
/// <remarks>
/// Not thread-safe by itself: every method must be called from the render
/// thread. <see cref="Rendering.RenderQueue"/> is the designated owner.
/// </remarks>
public sealed class PdfDocument : IDisposable
{
    private static long _nextId;

    private readonly SafePdfDocument _document;
    private readonly FileStream? _stream;
    private readonly PageSize[] _sizes;
    private bool _disposed;

    private PdfDocument(SafePdfDocument document, FileStream? stream, PageSize[] sizes, string? path, string? title, uint permissions)
    {
        Id = Interlocked.Increment(ref _nextId);
        _document = document;
        _stream = stream;
        _sizes = sizes;
        FilePath = path;
        Title = title;
        Permissions = permissions;
    }

    /// <summary>Process-unique document identifier used to scope cached renders.</summary>
    public long Id { get; }

    /// <summary>Full path of the open file, or null when loaded from memory.</summary>
    public string? FilePath { get; }

    /// <summary>Document title from metadata, when present.</summary>
    public string? Title { get; }

    public int PageCount => _sizes.Length;

    /// <summary>Raw FPDF_GetDocPermissions bitmask.</summary>
    public uint Permissions { get; }

    public PageSize GetPageSize(int pageIndex)
    {
        if ((uint)pageIndex >= (uint)_sizes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }
        return _sizes[pageIndex];
    }

    internal nint Handle => _document.DangerousGetHandle();

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Opens a document by streaming it, so a large file is not held in memory.
    /// Geometry for every page is resolved up front: this is what lets the UI
    /// lay out the scroll space without rendering anything.
    /// </summary>
    public static PdfOpenResult Open(string path, string? password = null)
    {
        PdfiumLibrary.EnsureInitialized();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return PdfOpenResult.Fail(PdfOpenError.NotFound);
        }

        FileStream stream;
        try
        {
            // Full share so another reader (or a sync client) is not blocked.
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (UnauthorizedAccessException)
        {
            return PdfOpenResult.Fail(PdfOpenError.AccessDenied);
        }
        catch (IOException)
        {
            return PdfOpenResult.Fail(PdfOpenError.AccessDenied);
        }

        try
        {
            var result = LoadFromStream(stream, password, path);
            if (!result.Success)
            {
                stream.Dispose();
            }
            return result;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static PdfOpenResult LoadFromStream(FileStream stream, string? password, string? path)
    {
        var length = stream.Length;
        if (length == 0)
        {
            return PdfOpenResult.Fail(PdfOpenError.InvalidFormat);
        }
        if (length > uint.MaxValue)
        {
            // FPDF_FILEACCESS carries a 32-bit length.
            return PdfOpenResult.Fail(PdfOpenError.Unsupported);
        }

        var access = new FileAccessContext(stream);
        var callback = new FpdfGetBlockCallback(access.ReadBlock);
        var accessStruct = new FpdfFileAccess
        {
            FileLength = (uint)length,
            GetBlock = Marshal.GetFunctionPointerForDelegate(callback),
            Param = IntPtr.Zero,
        };

        var accessPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FpdfFileAccess>());
        try
        {
            Marshal.StructureToPtr(accessStruct, accessPtr, false);
            var handle = PdfiumNative.FPDF_LoadCustomDocument(accessPtr, password);
            if (handle == IntPtr.Zero)
            {
                var code = PdfiumNative.FPDF_GetLastError();
                return PdfOpenResult.Fail(MapError(code, password), code);
            }

            var safe = new SafePdfDocument(handle);
            var pageCount = PdfiumNative.FPDF_GetPageCount(safe.DangerousGetHandle());
            if (pageCount <= 0)
            {
                safe.Dispose();
                return PdfOpenResult.Fail(PdfOpenError.InvalidFormat, PdfiumNative.ErrorPage);
            }

            var sizes = new PageSize[pageCount];
            var docHandle = safe.DangerousGetHandle();
            for (var i = 0; i < pageCount; i++)
            {
                if (PdfiumNative.FPDF_GetPageSizeByIndexF(docHandle, i, out var size) == 0
                    || !float.IsFinite(size.Width) || !float.IsFinite(size.Height)
                    || size.Width <= 0 || size.Height <= 0)
                {
                    // One broken page must not sink the whole document: keep a
                    // sane placeholder so navigation still works.
                    sizes[i] = new PageSize(595f, 842f);
                    continue;
                }
                sizes[i] = new PageSize(size.Width, size.Height);
            }

            var permissions = PdfiumNative.FPDF_GetDocPermissions(docHandle);
            var document = new PdfDocument(safe, stream, sizes, path, null, permissions);

            // The callback delegate and the access struct memory must outlive the
            // document: the constructor takes ownership of both.
            document.AttachNativeAccess(callback, accessPtr);
            return PdfOpenResult.Ok(document);
        }
        catch
        {
            Marshal.FreeHGlobal(accessPtr);
            throw;
        }
    }

    private FpdfGetBlockCallback? _accessCallback;
    private IntPtr _accessPtr;

    private void AttachNativeAccess(FpdfGetBlockCallback callback, IntPtr accessPtr)
    {
        _accessCallback = callback;
        _accessPtr = accessPtr;
    }

    private static PdfOpenError MapError(int code, string? passwordSupplied) => code switch
    {
        PdfiumNative.ErrorPassword => string.IsNullOrEmpty(passwordSupplied)
            ? PdfOpenError.PasswordRequired
            : PdfOpenError.WrongPassword,
        PdfiumNative.ErrorSecurity => PdfOpenError.Security,
        PdfiumNative.ErrorFormat => PdfOpenError.InvalidFormat,
        PdfiumNative.ErrorFile => PdfOpenError.NotFound,
        PdfiumNative.ErrorPage => PdfOpenError.InvalidFormat,
        _ => PdfOpenError.Unknown,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        // Order matters: the document holds pointers into the access struct and
        // calls the callback while closing, so free them only after it is gone.
        _document.Dispose();

        if (_accessPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_accessPtr);
            _accessPtr = IntPtr.Zero;
        }
        _accessCallback = null;

        _stream?.Dispose();
    }

    /// <summary>
    /// Read callback handed to PDFium. Kept as a small class so the stream and
    /// its own lock travel together with the delegate.
    /// </summary>
    private sealed class FileAccessContext
    {
        private readonly FileStream _stream;
        private readonly object _lock = new();

        public FileAccessContext(FileStream stream) => _stream = stream;

        public int ReadBlock(IntPtr param, uint position, IntPtr buffer, uint size)
        {
            lock (_lock)
            {
                try
                {
                    if (_stream.Position != position)
                    {
                        _stream.Seek(position, SeekOrigin.Begin);
                    }

                    // PDFium hands a raw buffer; read through a pooled array to
                    // avoid allocating per block.
                    var rented = System.Buffers.ArrayPool<byte>.Shared.Rent((int)size);
                    try
                    {
                        var read = _stream.Read(rented, 0, (int)size);
                        if (read > 0)
                        {
                            Marshal.Copy(rented, 0, buffer, read);
                        }
                        return read > 0 ? 1 : 0;
                    }
                    finally
                    {
                        System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                    }
                }
                catch (IOException)
                {
                    return 0;
                }
                catch (ObjectDisposedException)
                {
                    return 0;
                }
            }
        }
    }
}
