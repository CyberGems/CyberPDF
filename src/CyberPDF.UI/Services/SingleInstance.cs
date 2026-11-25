using System.Runtime.InteropServices;

namespace CyberPDF.UI.Services;

/// <summary>
/// Keeps one CyberPDF process per session. The first launch owns a named mutex
/// for its whole lifetime; later launches hand their file to the running window
/// and exit without showing anything.
/// </summary>
/// <remarks>
/// This matters for a reader specifically: Windows starts a new process for every
/// double-clicked document, so without this, opening three PDFs would leave three
/// copies running with their own cache, recent files and settings writes.
///
/// Ownership is decided with <see cref="Mutex.WaitOne(int)"/> on a mutex created
/// without initial ownership. The <c>createdNew</c> out value is not enough: it
/// reports whether this process created the kernel object, not whether it holds
/// it, and stays true for a process that created the mutex and then died before
/// releasing it.
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    /// <summary>Mutex identity. Local to the session: one desktop user, one app.</summary>
    public const string MutexName = "CyberPDF_SingleInstance";

    private const string ActivationMessageName = "CyberPDF_OpenDocument";

    /// <summary>WM_COPYDATA: the documented way to pass data to another process.</summary>
    internal const int WmCopyData = 0x004A;
    private const int MaxPathChars = 4096;

    /// <summary>Broadcast target used when the sender has no window of its own.</summary>
    private static readonly IntPtr HwndBroadcast = new(0xffff);

    private readonly Mutex _mutex;
    private readonly bool _owned;
    private IntPtr _hwnd;
    private string? _pendingFile;
    private Action<string>? _onOpenRequested;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            // WaitOne(0) succeeds for exactly one process: the one holding the
            // mutex at this instant. A crashed owner releases it automatically,
            // so a stale lock can never block the app from starting.
            _owned = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing. The mutex is ours now,
            // which is exactly what we want.
            _owned = true;
        }
    }

    /// <summary>True when this process is the one that should show a window.</summary>
    public bool IsFirstInstance => _owned;

    /// <summary>
    /// Starts listening for "open this file" messages from later launches.
    /// </summary>
    /// <remarks>
    /// The window is subclassed because WinForms only forwards messages it
    /// recognizes; a registered message would otherwise never reach any handler
    /// and a second launch would appear to do nothing.
    /// </remarks>
    public void Listen(Control window, Action<string> onOpenRequested)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(onOpenRequested);

        _onOpenRequested = onOpenRequested;
        _hwnd = window.Handle;


        // A file may have arrived while the window was still starting.
        if (_pendingFile is { } pending)
        {
            _pendingFile = null;
            onOpenRequested(pending);
        }
    }

    /// <summary>
    /// Processes a WM_COPYDATA sent by another instance. The window calls this
    /// from its own WndProc override, which is the one place a Form reliably sees
    /// messages before the class handler consumes them.
    /// </summary>
    /// <returns>True when the message was ours and has been handled.</returns>
    public bool TryHandleCopyData(ref Message message)
        => HandleMessage((uint)message.Msg, message.WParam, message.LParam);

    /// <summary>
    /// Hands a file to the running instance. A secondary process has no window of
    /// its own, so the message is broadcast; the running instance is the only
    /// listener for this registered message.
    /// </summary>
    public static void SendToRunningInstance(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var destination = FindOwnerWindow();
        Core.Diagnostics.AppLog.Info($"SingleInstance: handing '{filePath}' to the running window.");
        if (destination == IntPtr.Zero)
        {
            return;
        }

        // WM_COPYDATA carries the bytes and is delivered to the target's window
        // procedure by the OS, so no custom registration or hook is involved.
        var bytes = System.Text.Encoding.Unicode.GetBytes(filePath);
        var buffer = Marshal.AllocHGlobal(bytes.Length + sizeof(int));
        try
        {
            Marshal.WriteInt32(buffer, bytes.Length);
            Marshal.Copy(bytes, 0, buffer + sizeof(int), bytes.Length);

            var header = new CopyDataStruct
            {
                DwData = CopyDataMagic,
                CbData = bytes.Length + sizeof(int),
                LpData = buffer,
            };
            var headerPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataStruct>());
            try
            {
                Marshal.StructureToPtr(header, headerPtr, false);
                _ = SendMessageTimeout(
                    destination,
                    WmCopyData,
                    IntPtr.Zero,
                    headerPtr,
                    SmtoAbortIfHung,
                    3000,
                    out _);
            }
            finally
            {
                Marshal.FreeHGlobal(headerPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Finds the running instance's window so a secondary process can post to it
    /// directly. Broadcasting is unreliable here: a registered message is dropped
    /// for windows that have not subscribed to it.
    /// </summary>
    private static IntPtr FindOwnerWindow()
    {
        var found = IntPtr.Zero;
        var className = new char[256];
        EnumWindows((hwnd, _) =>
        {
            // Match the window class, not the title: another application (an
            // editor with this project's AGENTS.md open, for instance) can have
            // "CyberPDF" in its title, and posting the handoff to it would lose
            // the file silently.
            Array.Clear(className);
            var length = GetClassName(hwnd, className, className.Length);
            if (length == 0)
            {
                return true;
            }
            if (!new ReadOnlySpan<char>(className, 0, length).StartsWith(WindowClassNamePrefix, StringComparison.Ordinal))
            {
                return true;
            }

            // Skip processes that are gone but whose window outlives them. A
            // zero thread id means the window has no owning thread any more.
            var threadId = GetWindowThreadProcessId(hwnd, out var pid);
            if (threadId == 0 || pid == 0)
            {
                return true;
            }

            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr hwnd, [Out] char[] className, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    /// <summary>
    /// Called from the window hook for every message. Returns true when the
    /// message was ours and must not reach the default handling.
    /// </summary>
    internal bool HandleMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != WmCopyData || lParam == IntPtr.Zero)
        {
            return false;
        }

        var header = Marshal.PtrToStructure<CopyDataStruct>(lParam);
        if (header.DwData != CopyDataMagic || header.LpData == IntPtr.Zero || header.CbData <= sizeof(int))
        {
            return false;
        }

        var length = Marshal.ReadInt32(header.LpData);
        if (length <= 0 || length > header.CbData - sizeof(int))
        {
            return false;
        }

        var bytes = new byte[length];
        Marshal.Copy(header.LpData + sizeof(int), bytes, 0, length);
        var filePath = System.Text.Encoding.Unicode.GetString(bytes);
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            _onOpenRequested?.Invoke(filePath);
        }
        return true;
    }

    public void Dispose()
    {
        if (_owned)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not held by this thread; nothing to release.
            }
        }
        _mutex.Dispose();
    }

    private static IntPtr WriteSharedPath(string filePath)
    {
        try
        {
            var length = Math.Min(filePath.Length, MaxPathChars - 1);
            var block = Marshal.AllocHGlobal((length + 1) * sizeof(char));
            if (block == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var buffer = new char[length + 1];
            filePath.CopyTo(0, buffer, 0, length);
            buffer[length] = '\0';
            Marshal.Copy(buffer, 0, block, buffer.Length);
            return block;
        }
        catch (OutOfMemoryException)
        {
            return IntPtr.Zero;
        }
    }

    private static string? ReadSharedPath(IntPtr block)
    {
        if (block == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new char[MaxPathChars];
            Marshal.Copy(block, buffer, 0, buffer.Length);
            var length = Array.IndexOf(buffer, '\0');
            return length <= 0 ? null : new string(buffer, 0, length);
        }
        finally
        {
            // The receiver owns the block: whatever the outcome, it is freed here.
            Marshal.FreeHGlobal(block);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CopyDataStruct
    {
        public IntPtr DwData;
        public int CbData;
        public IntPtr LpData;
    }

    /// <summary>Identifies our payload so a stray WM_COPYDATA is ignored.</summary>
    private const int CopyDataMagic = 0x43504446; // "CPDF"

    private const int SmtoAbortIfHung = 0x0002;

    /// <summary>WinForms' top-level window class, used to identify our own window.</summary>
    private const string WindowClassNamePrefix = "WindowsForms10.Window";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
