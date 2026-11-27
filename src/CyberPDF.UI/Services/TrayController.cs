using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Shell;
using CyberPDF.UI.Controls;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Services;

/// <summary>
/// Manages CyberPDF's system tray icon and themed context menu following the
/// suite's CyberLauncher pattern: dynamic Show/Hide, Open PDF, Settings,
/// Most recent submenu, Help submenu, About/Update check, and Exit.
/// </summary>
public sealed class TrayController : IDisposable
{
    private const string RepoUrl = "https://github.com/CyberGems/CyberPDF";
    private const string WikiUrl = $"{RepoUrl}/wiki";
    private const string WikiEsUrl = $"{WikiUrl}/Home-es";
    private const string FaqUrl = $"{WikiUrl}/FAQ";
    private const string FaqEsUrl = $"{WikiUrl}/FAQ-es";
    private const string ReleasesUrl = $"{RepoUrl}/releases";
    private const string WebsiteUrl = "https://cybergems.org";
    private const string DonateUrl = $"{RepoUrl}#%EF%B8%8F-donate";

    private readonly IntPtr _ownerHandle;
    private readonly Func<bool> _isWindowVisible;
    private readonly Func<IReadOnlyList<string>> _getRecentFiles;
    private readonly Action _onToggleVisibility;
    private readonly Action _onShowAndOpen;
    private readonly Action<string> _onOpenRecent;
    private readonly Action _onSettings;
    private readonly Action<bool> _onAbout;
    private readonly Action _onExit;
    private readonly NotifyIcon _notifyIcon;

    private ThemedContextMenu? _activeMenu;
    private bool _disposed;

    public TrayController(
        IntPtr ownerHandle,
        Icon? icon,
        Func<bool> isWindowVisible,
        Func<IReadOnlyList<string>> getRecentFiles,
        Action onToggleVisibility,
        Action onShowAndOpen,
        Action<string> onOpenRecent,
        Action onSettings,
        Action<bool> onAbout,
        Action onExit)
    {
        _ownerHandle = ownerHandle;
        _isWindowVisible = isWindowVisible;
        _getRecentFiles = getRecentFiles;
        _onToggleVisibility = onToggleVisibility;
        _onShowAndOpen = onShowAndOpen;
        _onOpenRecent = onOpenRecent;
        _onSettings = onSettings;
        _onAbout = onAbout;
        _onExit = onExit;

        _notifyIcon = new NotifyIcon
        {
            Icon = icon ?? SystemIcons.Application,
            Text = TrayTooltipFormatter.Format(),
            Visible = true,
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;
    }

    public void UpdateTooltip(string? activeSegment)
    {
        if (_disposed)
        {
            return;
        }
        _notifyIcon.Text = TrayTooltipFormatter.Format(activeSegment);
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            ShowContextMenu();
        }
        else if (e.Button == MouseButtons.Left)
        {
            _onToggleVisibility();
        }
    }

    private void ShowContextMenu()
    {
        _activeMenu?.Close();
        _activeMenu?.Dispose();
        _activeMenu = null;

        var version = FormatAppVersion();
        var visible = _isWindowVisible();
        var isSpanish = Strings.Current == Lang.Es;

        var recents = BuildRecentItems();
        var helpItems = new List<ContextMenuItem>
        {
            new(Strings.Get("TrayPinIcon"), () => OpenExternal("ms-settings:taskbar"), IconKind.Pin),
            ContextMenuItem.Separator(),
            new(Strings.Get("TrayHelp"), () => OpenExternal(isSpanish ? WikiEsUrl : WikiUrl), IconKind.BookOpen),
            new(Strings.Get("TrayFaq"), () => OpenExternal(isSpanish ? FaqEsUrl : FaqUrl), IconKind.Help),
            new(Strings.Get("TrayChangelog"), () => OpenExternal(ReleasesUrl), IconKind.Tag),
            new(Strings.Get("TrayWebsite"), () => OpenExternal(WebsiteUrl), IconKind.Gem),
            new(Strings.Get("TrayDonate"), () => OpenExternal(DonateUrl), IconKind.Heart),
            ContextMenuItem.Separator(),
            new(Strings.Get("TrayAbout"), () => _onAbout(false), IconKind.Info),
            new(Strings.Get("TrayCheckUpdates"), () => _onAbout(true), IconKind.Reload),
        };

        _activeMenu = ThemedContextMenu.Create(
            new ContextMenuItem($"CyberPDF v{version}", () => _onAbout(false), IconKind.Gem),
            ContextMenuItem.Separator(),
            new ContextMenuItem(visible ? Strings.Get("TrayHide") : Strings.Get("TrayShow"), _onToggleVisibility, IconKind.ShowHide),
            new ContextMenuItem(Strings.Get("TrayOpenPdf"), _onShowAndOpen, IconKind.Open),
            new ContextMenuItem(Strings.Get("TraySettings"), _onSettings, IconKind.Settings),
            new ContextMenuItem(Strings.Get("TrayMostRecent"), null, IconKind.Recent, enabled: true, children: recents),
            new ContextMenuItem(Strings.Get("TrayHelp"), null, IconKind.Help, enabled: true, children: helpItems),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Strings.Get("TrayExit"), _onExit, IconKind.Exit));

        if (_ownerHandle != IntPtr.Zero)
        {
            SetForegroundWindow(_ownerHandle);
        }
        _activeMenu.Show(Cursor.Position);
    }

    private List<ContextMenuItem> BuildRecentItems()
    {
        var items = new List<ContextMenuItem>();
        var files = _getRecentFiles();
        var count = 0;
        foreach (var path in files)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }
            count++;
            var captured = path;
            items.Add(new ContextMenuItem(
                string.Create(CultureInfo.InvariantCulture, $"{count}. {Path.GetFileName(path)}"),
                () => _onOpenRecent(captured),
                IconKind.Document));
            if (count >= 8)
            {
                break;
            }
        }

        if (items.Count == 0)
        {
            items.Add(new ContextMenuItem(Strings.Get("TrayNoRecents"), null, IconKind.None, enabled: false));
        }
        return items;
    }

    private static string FormatAppVersion()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver is null
            ? "0.3.1"
            : string.Create(CultureInfo.InvariantCulture, $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}");
    }

    private static void OpenExternal(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (SystemException ex)
        {
            Core.Diagnostics.AppLog.Warn($"Tray OpenExternal failed for {target}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _activeMenu?.Dispose();
        _activeMenu = null;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
