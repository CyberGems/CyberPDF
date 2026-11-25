using System.Reflection;
using System.Runtime.InteropServices;
using CyberPDF.Common.Settings;

namespace CyberPDF.Core.Interop;

/// <summary>
/// Loads pdfium.dll and owns its global lifetime. PDFium must be initialized
/// once before any other call and destroyed once at exit; doing it per document
/// leaks the font cache and is measurably slower on reopen.
/// </summary>
/// <remarks>
/// PDFium is not thread-safe. This type only guarantees a single initialization;
/// callers must serialize every other call through <see cref="Rendering.RenderQueue"/>.
/// </remarks>
public static class PdfiumLibrary
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static string? _loadedFrom;
    private static bool _resolverInstalled;

    /// <summary>True once PDFium has been initialized successfully.</summary>
    public static bool IsInitialized
    {
        get { lock (Gate) { return _initialized; } }
    }

    /// <summary>Directory the DLL was loaded from, for diagnostics.</summary>
    public static string? LoadedFrom
    {
        get { lock (Gate) { return _loadedFrom; } }
    }

    /// <summary>
    /// Initializes PDFium, probing the development folder when the DLL is not
    /// beside the executable. Throws <see cref="DllNotFoundException"/> with a
    /// readable message when the DLL cannot be located at all.
    /// </summary>
    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            // A resolver, not NativeLibrary.Load: loading by path and then letting
            // the P/Invoke layer resolve "pdfium" by name would load two separate
            // copies of the DLL, each with its own global state. The resolver
            // points every "pdfium" import at the one verified file.
            InstallResolver();

            PdfiumNative.FPDF_InitLibrary();

            // Documents must not read the wall clock: two renders of the same
            // page have to produce identical pixels.
            PdfiumNative.FPDF_SetSandBoxPolicy(PdfiumNative.PolicyMachineTimeAccess, false);

            _initialized = true;
        }
    }

    private static void InstallResolver()
    {
        if (_resolverInstalled)
        {
            return;
        }

        var resolved = ResolveLibraryPath();
        _loadedFrom = resolved is null ? AppContext.BaseDirectory : Path.GetDirectoryName(resolved);

        NativeLibrary.SetDllImportResolver(typeof(PdfiumLibrary).Assembly, (name, assembly, searchPath) =>
        {
            if (!string.Equals(name, PdfiumNative.Library, StringComparison.OrdinalIgnoreCase))
            {
                return IntPtr.Zero;
            }

            if (resolved is not null && NativeLibrary.TryLoad(resolved, out var handle))
            {
                return handle;
            }

            return NativeLibrary.TryLoad(name, assembly, searchPath, out var fallback) ? fallback : IntPtr.Zero;
        });

        _resolverInstalled = true;
    }

    private static string? ResolveLibraryPath()
    {
        const string fileName = "pdfium.dll";
        foreach (var directory in AppPaths.NativeLibraryProbes())
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // A published build keeps the DLL beside the exe, which the default
        // search order already covers, but a missing DLL should fail with a
        // clear message instead of a raw loader error.
        var beside = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(beside))
        {
            return beside;
        }

        throw new DllNotFoundException(
            "pdfium.dll was not found. Run scripts/Fetch-Pdfium.ps1 to restore the native engine.");
    }
}
