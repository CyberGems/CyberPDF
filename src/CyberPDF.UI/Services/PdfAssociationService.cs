using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CyberPDF.UI.Services;

public enum DefaultPdfResult
{
    IsNowDefault,
    OpenedWindowsSettings,
    Failed,
}

/// <summary>
/// Registers CyberPDF per-user in the Windows registry (HKCU, no elevation
/// needed) and checks whether it is the active default handler for <c>.pdf</c>.
/// On Windows 10 and 11, switching away from another reader's locked UserChoice
/// requires user confirmation in Windows Default Apps, so this service registers
/// Capabilities and opens the Settings page directly when needed.
/// </summary>
public static class PdfAssociationService
{
    public const string ProgId = "CyberPDF.Document";
    private const string RegisteredAppName = "CyberPDF";
    private const string CapabilitiesSubkey = @"Software\CyberGems\CyberPDF\Capabilities";

    private const uint AssocfNone = 0;
    private const int AssocstrCommand = 1;
    private const int AssocstrExecutable = 2;
    private const int AssocstrProgId = 20;
    private const uint ShcneAssocChanged = 0x08000000;
    private const uint ShcnfIdList = 0x0000;

    /// <summary>
    /// True when Windows currently resolves <c>.pdf</c> open commands to
    /// CyberPDF's ProgID or to the running executable.
    /// </summary>
    public static bool IsDefaultPdfHandler()
    {
        try
        {
            var progId = QueryAssociation(AssocstrProgId);
            if (string.Equals(progId, ProgId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var currentExe = NormalizePath(Environment.ProcessPath);
            var associatedExe = NormalizePath(QueryAssociation(AssocstrExecutable));
            if (!string.IsNullOrEmpty(currentExe) &&
                string.Equals(associatedExe, currentExe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var userChoice = ReadUserChoiceProgId();
            if (!string.IsNullOrEmpty(userChoice))
            {
                return string.Equals(userChoice, ProgId, StringComparison.OrdinalIgnoreCase);
            }

            // Last check for environments where AssocQueryString did not resolve UserChoice.
            var command = QueryAssociation(AssocstrCommand);
            if (!string.IsNullOrEmpty(currentExe) &&
                command.Contains(currentExe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is SystemException or UnauthorizedAccessException)
        {
            Core.Diagnostics.AppLog.Warn($"Default PDF query failed: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Writes per-user ProgID, application capability metadata, and fallback
    /// <c>.pdf</c> association keys under <c>HKCU</c>.
    /// </summary>
    public static void RegisterCurrentExecutable(string? executablePath = null, bool claimFallbackClass = true)
    {
        var exePath = NormalizePath(executablePath ?? Environment.ProcessPath);
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        var exeName = Path.GetFileName(exePath);

        using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
        {
            progIdKey?.SetValue(string.Empty, "PDF document");
            progIdKey?.SetValue("FriendlyTypeName", "PDF document");

            using var defaultIcon = progIdKey?.CreateSubKey("DefaultIcon");
            defaultIcon?.SetValue(string.Empty, $"\"{exePath}\",0");

            using var appMeta = progIdKey?.CreateSubKey("Application");
            appMeta?.SetValue("ApplicationName", "CyberPDF");
            appMeta?.SetValue("ApplicationDescription", "Fast, lightweight PDF reader");

            using var cmd = progIdKey?.CreateSubKey(@"shell\open\command");
            cmd?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }

        using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{exeName}"))
        {
            appKey?.SetValue("FriendlyAppName", "CyberPDF");
            using var supported = appKey?.CreateSubKey("SupportedTypes");
            supported?.SetValue(".pdf", string.Empty);
            using var cmd = appKey?.CreateSubKey(@"shell\open\command");
            cmd?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }

        using (var pdfKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf"))
        {
            if (claimFallbackClass)
            {
                pdfKey?.SetValue(string.Empty, ProgId);
            }
            using var openWith = pdfKey?.CreateSubKey("OpenWithProgids");
            openWith?.SetValue(ProgId, string.Empty);
        }

        using (var capKey = Registry.CurrentUser.CreateSubKey(CapabilitiesSubkey))
        {
            capKey?.SetValue("ApplicationName", "CyberPDF");
            capKey?.SetValue("ApplicationDescription", "Fast, lightweight PDF reader");
            using var assocs = capKey?.CreateSubKey("FileAssociations");
            assocs?.SetValue(".pdf", ProgId);
        }

        using (var regApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
        {
            regApps?.SetValue(RegisteredAppName, CapabilitiesSubkey);
        }

        SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Ensures CyberPDF is registered for <c>.pdf</c>. If Windows already
    /// gives priority to another handler via UserChoice, opens Windows Default
    /// Apps so the user can confirm CyberPDF directly.
    /// </summary>
    public static DefaultPdfResult SetAsDefaultPdfHandler()
    {
        try
        {
            RegisterCurrentExecutable(claimFallbackClass: true);
            if (IsDefaultPdfHandler())
            {
                return DefaultPdfResult.IsNowDefault;
            }

            if (TryLaunchSettings("ms-settings:defaultapps?registeredAppUser=CyberPDF")
                || TryLaunchSettings("ms-settings:defaultapps"))
            {
                return DefaultPdfResult.OpenedWindowsSettings;
            }
        }
        catch (Exception ex) when (ex is SystemException or UnauthorizedAccessException)
        {
            Core.Diagnostics.AppLog.Warn($"Default PDF setup failed: {ex.Message}");
        }

        return DefaultPdfResult.Failed;
    }

    private static bool TryLaunchSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }
        catch (SystemException)
        {
            return false;
        }
    }

    private static string? ReadUserChoiceProgId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.pdf\UserChoice");
        return key?.GetValue("ProgId") as string;
    }

    private static string QueryAssociation(int strType)
    {
        uint size = 0;
        _ = AssocQueryString(AssocfNone, strType, ".pdf", "open", null, ref size);
        if (size <= 1 || size > 4096)
        {
            return string.Empty;
        }

        var buffer = new char[size];
        var hr = AssocQueryString(AssocfNone, strType, ".pdf", "open", buffer, ref size);
        if (hr < 0 || size == 0)
        {
            return string.Empty;
        }

        var length = (int)Math.Min(size, (uint)buffer.Length);
        return new string(buffer, 0, length).TrimEnd('\0');
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }
        try
        {
            return Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim().Trim('"');
        }
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(
        uint flags,
        int str,
        string pszAssoc,
        string? pszExtra,
        [Out] char[]? pszOut,
        ref uint pcchOut);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
