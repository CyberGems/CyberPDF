using System.Diagnostics;
using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;
using CyberPDF.UI.Controls;
using CyberPDF.UI.Services;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI;

/// <summary>
/// Main reader window: a toolbar, the page surface and a status bar. Frameless
/// with a custom title strip, matching the suite's chrome.
/// </summary>
public sealed class MainWindow : Form
{
    private const int TitleBarHeight = 34;
    private const int ToolbarHeight = 44;
    private const int StatusBarHeight = 28;

    private readonly PdfEngine _engine;
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;

    private readonly Panel _titleBar = new();
    private readonly Panel _toolbar = new();
    private readonly Panel _statusBar = new();
    private readonly PageViewer _viewer;
    private readonly ThinScrollBar _scrollBar = new();
    private readonly Label _pageLabel = new();
    private readonly Label _zoomLabel = new();
    private readonly Label _titleLabel = new();
    private readonly ToolButton _openButton = new();
    private readonly ToolButton _zoomOutButton = new();
    private readonly ToolButton _zoomInButton = new();
    private readonly ToolButton _fitWidthButton = new();
    private readonly ToolButton _rotateLeftButton = new();
    private readonly ToolButton _rotateRightButton = new();
    private readonly ToolButton _themeButton = new();
    private readonly ToolButton _copyButton = new();
    private readonly ToolButton _searchButton = new();
    private readonly Panel _searchBar = new();
    private readonly TextBox _searchField = new();
    private readonly Label _searchResults = new();
    private readonly ToolButton _searchPreviousButton = new();
    private readonly ToolButton _searchNextButton = new();
    private readonly ToolButton _searchCloseButton = new();
    private readonly ToolButton _minimizeButton = new();
    private readonly ToolButton _maximizeButton = new();
    private readonly ToolButton _closeButton = new();
    private readonly ToolButton _sidePanelButton = new();
    private readonly SidePanel _sidePanel;
    private ThemedToolTip? _toolTip;

    private PdfDocument? _document;
    private bool _diagRequested;
    private bool _stressRequested;
    private string? _documentPath;
    private float _zoom = 1f;
    private int _rotation;
    private bool _updatingScrollBar;

    public MainWindow(PdfEngine engine, AppSettings settings, SettingsStore store)
    {
        _engine = engine;
        _settings = settings;
        _store = store;
        _viewer = new PageViewer(engine);
        _sidePanel = new SidePanel(engine);

        SuspendLayout();

        Text = Strings.Get("AppTitle");

        // The layout is authored at 96 DPI and WinForms scales every bound and
        // the base font from there. Without this, a 144 DPI monitor (150%)
        // renders hardcoded heights too small and clips captions.
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 420);

        Size = new Size(1100, 820);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        DoubleBuffered = true;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        AllowDrop = true;

        BuildTitleBar();
        BuildToolbar();
        BuildSearchBar();
        BuildStatusBar();
        BuildLayout();
        BuildToolTips();

        ResumeLayout(false);

        WireEvents();
        ApplyTheme();
        UpdateCommandStates();
        UpdateStatus();
    }

    // --- Construction -------------------------------------------------------

    private void BuildTitleBar()
    {
        _titleBar.Dock = DockStyle.Top;
        _titleBar.Height = TitleBarHeight;
        _titleBar.BackColor = Theme.Bg;

        _titleLabel.AutoSize = false;
        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _titleLabel.Padding = new Padding(12, 0, 0, 0);
        _titleLabel.ForeColor = Theme.Text;
        _searchBar.BackColor = Theme.Surface;
        _sidePanel.BackColor = Theme.Surface;
        _searchField.BackColor = Theme.Canvas;
        _searchField.ForeColor = Theme.Text;
        _searchResults.ForeColor = Theme.TextMuted;
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.AutoEllipsis = true;
        _titleLabel.MouseDown += TitleBarMouseDown;
        _titleLabel.MouseDoubleClick += (_, _) => ToggleMaximize();

        _titleBar.MouseDown += TitleBarMouseDown;
        _titleBar.MouseDoubleClick += (_, _) => ToggleMaximize();

        ConfigureWindowButton(_themeButton, IconKind.ThemeDefault, Strings.Get("ChangeTheme"), CycleTheme);
        ConfigureWindowButton(_minimizeButton, IconKind.Minimize, Strings.Get("Minimize"),
            () => WindowState = FormWindowState.Minimized);
        ConfigureWindowButton(_maximizeButton, IconKind.Maximize, Strings.Get("Maximize"), ToggleMaximize);
        ConfigureWindowButton(_closeButton, IconKind.Close, Strings.Get("CloseWindow"), Close);

        _titleBar.Controls.Add(_titleLabel);

        // Right-docked, the control added last sits closest to the edge, so the
        // close button goes in last to keep the suite's minimize/maximize/close.
        _titleBar.Controls.Add(_themeButton);
        _titleBar.Controls.Add(_minimizeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_closeButton);
    }

    private static void ConfigureWindowButton(ToolButton button, IconKind icon, string accessibleName, Action onClick)
    {
        button.Icon = icon;
        button.Dock = DockStyle.Right;
        button.Width = 46;
        button.Height = TitleBarHeight;
        button.AccessibleName = accessibleName;
        button.Click += (_, _) => onClick();
    }

    private void BuildToolbar()
    {
        _toolbar.Dock = DockStyle.Top;
        _toolbar.Height = ToolbarHeight;
        _toolbar.BackColor = Theme.Surface;
        _toolbar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1);
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.Transparent,
        };

        ConfigureButton(_openButton, IconKind.Open, Strings.Get("Open"), OpenFile);
        ConfigureButton(_zoomOutButton, IconKind.ZoomOut, null, () => ChangeZoom(1 / 1.25f));
        ConfigureButton(_zoomInButton, IconKind.ZoomIn, null, () => ChangeZoom(1.25f));
        ConfigureButton(_fitWidthButton, IconKind.FitWidth, null, FitWidth);
        ConfigureButton(_rotateLeftButton, IconKind.RotateLeft, null, () => Rotate(-90));
        ConfigureButton(_rotateRightButton, IconKind.RotateRight, null, () => Rotate(90));
        ConfigureButton(_copyButton, IconKind.Copy, Strings.Get("Copy"), CopySelection);
        _copyButton.Enabled = false;
        ConfigureButton(_searchButton, IconKind.Search, null, OpenSearch);
        ConfigureButton(_sidePanelButton, IconKind.SidePanel, null, ToggleSidePanel);

        flow.Controls.Add(_openButton);
        flow.Controls.Add(_sidePanelButton);
        flow.Controls.Add(Separator());
        flow.Controls.Add(_zoomOutButton);
        flow.Controls.Add(_zoomInButton);
        flow.Controls.Add(_fitWidthButton);
        flow.Controls.Add(Separator());
        flow.Controls.Add(_rotateLeftButton);
        flow.Controls.Add(_rotateRightButton);
        flow.Controls.Add(_copyButton);
        flow.Controls.Add(Separator());
        flow.Controls.Add(_searchButton);

        _toolbar.Controls.Add(flow);
    }

    private static Panel Separator() => new Panel
    {
        Width = 1,
        Height = 22,
        Margin = new Padding(6, 3, 6, 3),
        BackColor = Theme.Border,
    };

    private static void ConfigureButton(ToolButton button, IconKind icon, string? text, Action onClick)
    {
        button.Icon = icon;
        if (text is not null)
        {
            button.Text = text;
        }
        button.Margin = new Padding(1, 0, 1, 0);
        button.Size = text is null ? new Size(34, 32) : button.MeasurePreferredSize();
        button.AccessibleName = text ?? icon.ToString();
        button.Click += (_, _) => onClick();
    }

    private void BuildSearchBar()
    {
        _searchBar.Dock = DockStyle.Top;
        _searchBar.Height = 40;
        _searchBar.BackColor = Theme.Surface;
        _searchBar.Visible = false;
        _searchBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, _searchBar.Height - 1, _searchBar.Width, _searchBar.Height - 1);
        };

        // A real TextBox: it gives caret, selection and the suite's own context
        // menu for editable fields without reimplementing any of it.
        _searchField.BorderStyle = BorderStyle.FixedSingle;
        _searchField.BackColor = Theme.Canvas;
        _searchField.ForeColor = Theme.Text;
        _searchField.Width = 260;
        _searchField.PlaceholderText = Strings.Get("SearchPlaceholder");
        _searchField.TextChanged += (_, _) => RunSearch();
        _searchField.KeyDown += OnSearchFieldKeyDown;
        _searchField.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                TextFieldContextMenu.Show(_searchField, Cursor.Position);
            }
        };

        _searchResults.AutoSize = false;
        _searchResults.Width = 110;
        _searchResults.TextAlign = ContentAlignment.MiddleLeft;
        _searchResults.ForeColor = Theme.TextMuted;
        _searchResults.BackColor = Color.Transparent;

        ConfigureButton(_searchPreviousButton, IconKind.PreviousPage, null, () => _viewer.GoToPreviousMatch());
        ConfigureButton(_searchNextButton, IconKind.NextPage, null, () => _viewer.GoToNextMatch());
        ConfigureButton(_searchCloseButton, IconKind.Close, null, CloseSearch);
        _searchCloseButton.AccessibleName = Strings.Get("SearchClear");

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10, 4, 10, 4),
            BackColor = Color.Transparent,
        };
        layout.Controls.Add(_searchField);
        layout.Controls.Add(_searchResults);
        layout.Controls.Add(_searchPreviousButton);
        layout.Controls.Add(_searchNextButton);
        layout.Controls.Add(_searchCloseButton);

        _searchBar.Controls.Add(layout);
    }

    private void BuildStatusBar()
    {
        _statusBar.Dock = DockStyle.Bottom;
        _statusBar.Height = StatusBarHeight;
        _statusBar.BackColor = Theme.Surface;
        _statusBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 0, 0, _statusBar.Width, 0);
        };

        _pageLabel.AutoSize = false;
        _pageLabel.Dock = DockStyle.Left;
        _pageLabel.Width = 260;
        _pageLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pageLabel.Padding = new Padding(14, 0, 0, 0);
        _pageLabel.ForeColor = Theme.TextMuted;
        _pageLabel.BackColor = Color.Transparent;

        _zoomLabel.AutoSize = false;
        _zoomLabel.Dock = DockStyle.Right;
        _zoomLabel.Width = 90;
        _zoomLabel.TextAlign = ContentAlignment.MiddleRight;
        _zoomLabel.Padding = new Padding(0, 0, 14, 0);
        _zoomLabel.ForeColor = Theme.TextMuted;
        _zoomLabel.BackColor = Color.Transparent;

        _statusBar.Controls.Add(_zoomLabel);
        _statusBar.Controls.Add(_pageLabel);
    }

    /// <summary>
    /// Enables the hidden diagnostics dump, emitted once the window is shown and
    /// the first document (if any) has had time to render.
    /// </summary>
    /// <param name="stress">
    /// Scroll and zoom through the document before dumping, which is where a leak
    /// in the render pipeline becomes visible.
    /// </param>
    public void RequestDiagnostics(bool stress = false)
    {
        _diagRequested = true;
        _stressRequested = stress;
    }

    /// <summary>
    /// Schedules the dump. Runs after <see cref="Shown"/> so the window has real
    /// dimensions, and waits long enough for the render queue to settle: bands
    /// arrive over several frames, so an immediate reading would report almost no
    /// memory and hide real growth.
    /// </summary>
    private void ScheduleDiagnosticsDump()
    {
        if (!_diagRequested)
        {
            return;
        }

        var timer = new System.Windows.Forms.Timer { Interval = _stressRequested ? 800 : 3000 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            WriteDiagnosticsDump();
            Close();
        };
        timer.Start();
    }

    private void WriteDiagnosticsDump()
    {
        try
        {
            if (_stressRequested)
            {
                RunStressPass();
            }

            var report = DiagnosticsProbe.Build(this, CaptureInfo());

            // A WinExe has no console attached, so Console.WriteLine would go
            // nowhere. stderr is still capturable by a caller, and the optional
            // --diag-out file is the reliable channel.
            Console.Error.WriteLine(report);
            Console.Error.Flush();

            var outFile = DiagnosticsProbe.OutputFile(Environment.GetCommandLineArgs());
            if (outFile is not null)
            {
                File.WriteAllText(outFile, report);
            }
            System.Diagnostics.Debug.WriteLine(report);
        }
        catch (Exception ex)
        {
            // The dump runs unattended; a failure has to be visible instead of
            // leaving the process alive with no output.
            Core.Diagnostics.AppLog.Error("Diagnostics dump failed", ex);
            Console.Error.WriteLine("DIAG-ERROR: " + ex);
        }
    }

    /// <summary>The document surface, exposed for diagnostics and screenshots.</summary>
    internal PageViewer DocumentViewer => _viewer;

    /// <summary>True once a document is open, for diagnostics and screenshots.</summary>
    internal bool HasDocument => _document is not null;

    /// <summary>Current status bar text, for diagnostics.</summary>
    internal string StatusText => _pageLabel.Text;

    internal PdfDocumentInfo CaptureInfo()
    {
        var slots = _viewer.LayoutSlots;
        var first = slots.Count > 0 ? slots[0] : default;
        var size = _document?.GetPageSize(0) ?? default;
        var dpr = _viewer.DeviceDpi / 96f;
        var buttons = _toolbar.Controls.OfType<FlowLayoutPanel>()
            .SelectMany(f => f.Controls.OfType<ToolButton>())
            .Select(b => (b.Text, b.Width, b.Height, b.Enabled))
            .ToList();

        return new PdfDocumentInfo(
            _document?.PageCount ?? 0,
            size.Width,
            size.Height,
            _viewer.Zoom,
            first.Width,
            first.Height,
            first.Top,
            _viewer.ContentWidth,
            _viewer.ContentHeight,
            _viewer.DeviceDpi,
            _viewer.ClientSize.Width,
            _viewer.ClientSize.Height,
            _scrollBar.Maximum,
            _scrollBar.Value,
            _scrollBar.LargeChange,
            first.Width,
            first.Height,
            Math.Max(1, (int)Math.Ceiling((double)first.Height / 1280.0)),
            1280,
            Math.Min(1280, first.Height),
            _scrollBar.Bounds,
            _titleBar.Bounds,
            _toolbar.Bounds,
            _searchBar.Bounds,
            _statusBar.Bounds,
            _viewer.Bounds,
            _searchBar.Parent?.Name ?? "(none)",
            _pageLabel.Text,
            _titleLabel.Text,
            _viewer.TileCount,
            _viewer.TileBytes / 1048576.0,
            _viewer.CacheCount,
            _viewer.CacheBytes / 1048576.0,
            (int)(_engine.Cache.BudgetBytes / 1048576),
            Process.GetCurrentProcess().PrivateMemorySize64 / 1048576.0,
            Process.GetCurrentProcess().WorkingSet64 / 1048576.0,
            buttons);
    }

    /// <summary>
    /// Hidden verification entry point (<c>--selftest</c>): selects the text of
    /// the first page programmatically, reports what the selection produced and
    /// copies it, so selection can be checked without driving the mouse.
    /// </summary>
    public async Task RunSelectionSelfTestAsync()
    {
        if (_document is null)
        {
            Console.Error.WriteLine("SELFTEST: no document open");
            return;
        }

        RunShellSelfTest();
        _viewer.SelectAllOnCurrentPage();

        // Extraction runs on the PDFium thread; wait for the selection to arrive.
        for (var i = 0; i < 40 && !_viewer.HasSelection; i++)
        {
            await Task.Delay(100);
        }

        var text = _viewer.GetSelectedText();
        Console.Error.WriteLine($"SELFTEST: hasSelection={_viewer.HasSelection} chars={text.Length}");
        Console.Error.WriteLine($"SELFTEST: head={text.Replace(Environment.NewLine, " | ")[..Math.Min(120, text.Length)]}");

        if (text.Length > 0)
        {
            CopySelection();

            // The clipboard is written by this same process, so reading it back
            // proves the command actually completed.
            var readBack = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            Console.Error.WriteLine($"SELFTEST: clipboardChars={readBack.Length} matches={readBack == text}");
        }

        await RunSearchSelfTestAsync();
    }

    /// <summary>
    /// Reports the shell geometry and drives the wheel and the drag, so the four
    /// defects reported after v0.1.0 stay fixed and a regression shows up as a
    /// number instead of a screenshot that has to be squinted at.
    /// </summary>
    private void RunShellSelfTest()
    {
        // 1. The viewer must not span the whole window: with the old dock order
        //    its client area equalled the form and the chrome overlapped it.
        Console.Error.WriteLine(
            $"SHELL: form={ClientSize.Width}x{ClientSize.Height} " +
            $"viewer={_viewer.ClientSize.Width}x{_viewer.ClientSize.Height} " +
            $"viewerAt={_viewer.Location} title={_titleBar.Bounds} toolbar={_toolbar.Bounds} " +
            $"status={_statusBar.Bounds} scroll={_scrollBar.Bounds}");

        var viewerInsideForm = _viewer.Bottom <= _statusBar.Top
                               && _viewer.Width < ClientSize.Width
                               && _viewer.Left >= 0;
        Console.Error.WriteLine($"SHELL: viewerInsideChrome={viewerInsideForm}");

        // 2. The search bar must be parented and land between toolbar and page.
        Console.Error.WriteLine(
            $"SHELL: searchParent={_searchBar.Parent?.GetType().Name ?? "(none)"} " +
            $"searchVisible={_searchBar.Visible}");

        // 3. The wheel: a notch from the window must move the scroll position,
        //    including when the focus sits on a toolbar button, which is where
        //    it lands after clicking one and where the old build stopped
        //    scrolling because the wheel followed the focus.
        _viewer.ScrollTo(0);
        var before = _viewer.ScrollY;
        OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120));
        Console.Error.WriteLine($"SHELL: wheel {before} -> {_viewer.ScrollY} delta={_viewer.ScrollY - before}");
        OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120));

        _zoomInButton.Focus();
        _viewer.ScrollTo(0);
        before = _viewer.ScrollY;
        PostMessage(_zoomInButton.Handle, 0x020A, (IntPtr)(-120 << 16), IntPtr.Zero);
        Application.DoEvents();
        Console.Error.WriteLine($"SHELL: wheelFromFocusedButton {before} -> {_viewer.ScrollY} " +
                                $"delta={_viewer.ScrollY - before} focus={ActiveControl?.GetType().Name}");
        _viewer.ScrollTo(0);
        _viewer.Focus();

        // 4. The drag: an actual cursor drag on the title strip. Synthetic
        //    messages do not start the native move loop, so this presses and
        //    moves the real pointer; a handler that never fires leaves the
        //    window where it was.
        var originalLocation = Location;
        var originalCursor = Cursor.Position;
        var grab = _titleLabel.PointToScreen(new Point(_titleLabel.Width / 2, _titleLabel.Height / 2));
        SetCursorPos(grab.X, grab.Y);
        Application.DoEvents();

        // The window enters a modal move loop on the UI thread when the title is
        // clicked, so a helper thread has to drive the pointer: code on the UI
        // thread would be stuck inside that loop and never move the cursor.
        var driver = new Thread(() =>
        {
            Thread.Sleep(150);
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            for (var step = 1; step <= 8; step++)
            {
                SetCursorPos(grab.X - 15 * step, grab.Y + 8 * step);
                Thread.Sleep(40);
            }
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        })
        {
            IsBackground = true,
        };
        driver.Start();

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && _moveLoopExits == 0)
        {
            Application.DoEvents();
            Thread.Sleep(20);
        }
        driver.Join(500);
        Application.DoEvents();

        var moved = Location != originalLocation;
        Console.Error.WriteLine(
            $"SHELL: drag moved={moved} from={originalLocation} to={Location} " +
            $"moveLoop entered={_moveLoopEntries} exited={_moveLoopExits}");

        Location = originalLocation;
        Cursor.Position = originalCursor;
        Application.DoEvents();

        // 5. The side panel: opening it must read the outline and render at
        //    least one miniature, on a document that has both.
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--panel", StringComparison.OrdinalIgnoreCase)))
        {
            ToggleSidePanel();
            for (var i = 0; i < 60 && _sidePanel.BookmarkCount == 0 && _sidePanel.ThumbnailCount == 0; i++)
            {
                Application.DoEvents();
                Thread.Sleep(100);
            }
            Console.Error.WriteLine(
                $"SHELL: panel visible={_sidePanel.Visible} bookmarks={_sidePanel.BookmarkCount} " +
                $"thumbs={_sidePanel.ThumbnailCount} mode={_sidePanel.Mode} " +
                $"panelBounds={_sidePanel.Bounds} viewerWidth={_viewer.ClientSize.Width}");
        }

        // 6. Toggling the panel mid-document must keep the page under the reader.
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--panel-scroll", StringComparison.OrdinalIgnoreCase)))
        {
            _viewer.ScrollToPage(_viewer.LayoutSlots.Count > 3 ? 3 : 0);
            Application.DoEvents();
            var pageBefore = _viewer.FirstVisiblePage;
            var scrollBefore = _viewer.ScrollY;
            var wasOpen = _sidePanel.Visible;
            SetSidePanelVisible(!wasOpen, persist: false);
            Application.DoEvents();
            Console.Error.WriteLine(
                $"SHELL: togglePanel page {pageBefore}->{_viewer.FirstVisiblePage} " +
                $"scroll {scrollBefore}->{_viewer.ScrollY} frame={_viewer.ClientSize.Width}x{_viewer.ClientSize.Height}");
            SetSidePanelVisible(wasOpen, persist: false);
        }

        // 7. Per-document panel state: a document that chose closed must not
        //    follow the global preference left open by another file.
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--panel-per-doc", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine(
                $"SHELL: perDoc file='{Path.GetFileName(_documentPath)}' " +
                $"panelVisible={_sidePanel.Visible} mode={_sidePanel.Mode} " +
                $"stored={_settings.ViewStates.GetValueOrDefault(NormalizePath(_documentPath!))?.SidePanelOpen?.ToString() ?? "(none)"}");
        }

        // 8. Panel persistence: the state is written and read back from the same
        //    file the next launch uses.
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--panel-persist", StringComparison.OrdinalIgnoreCase)))
        {
            ShowSidePanel(1);
            _sidePanel.Width = 240;
            _settings.SidePanelWidth = 240;
            _settings.SidePanelTab = 1;
            _settings.SidePanelOpen = true;
            _store.Save(_settings);

            var reloaded = new SettingsStore(_store.FilePath).Load();
            Console.Error.WriteLine(
                $"SHELL: persist open={reloaded.SidePanelOpen} tab={reloaded.SidePanelTab} " +
                $"width={reloaded.SidePanelWidth} applied={_sidePanel.Width} mode={_sidePanel.Mode}");
        }

        // 7. Tooltips are attached to every icon-only control, and the tip for
        //    the first toolbar button paints with the current palette.
        Console.Error.WriteLine($"SHELL: tooltips={_toolTip is not null} attachments={_toolTip?.Count ?? 0}");
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--tooltip-shot", StringComparison.OrdinalIgnoreCase)))
        {
            _toolTip!.ShowNow(_zoomInButton);
            Application.DoEvents();
            Thread.Sleep(250);
            _toolTip.CaptureTo(Path.Combine(Path.GetTempPath(), "cyberpdf-tooltip-shot.png"));
        }
    }

    /// <summary>
    /// Searches a known word and reports the match count and navigation, so the
    /// search pipeline can be verified without driving the UI.
    /// </summary>
    private async Task RunSearchSelfTestAsync()
    {
        // The term comes from --selftest-term when provided, so the same hook can
        // verify any document instead of only the one with "Website" in it.
        var termArg = Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith("--selftest-term=", StringComparison.OrdinalIgnoreCase));
        var term = termArg is null ? "Website" : termArg.Substring("--selftest-term=".Length);

        OpenSearch();
        _searchField.Text = term;
        RunSearch();

        // Pages are extracted one by one on the PDFium thread. Wait until the
        // count stops changing, not until it is non-zero, or the report only
        // covers the first page that happened to finish.
        var lastCount = -1;
        for (var i = 0; i < 100; i++)
        {
            await Task.Delay(100);
            if (_viewer.MatchCount == lastCount)
            {
                break;
            }
            lastCount = _viewer.MatchCount;
        }

        Console.Error.WriteLine($"SELFTEST: term='{term}' matches={_viewer.MatchCount} active={_viewer.ActiveMatchNumber}");
        Console.Error.WriteLine($"SELFTEST: results='{_searchResults.Text}'");

        if (_viewer.MatchCount > 1)
        {
            _viewer.GoToNextMatch();
            await Task.Delay(200);
            Console.Error.WriteLine($"SELFTEST: after next active={_viewer.ActiveMatchNumber} scroll={_viewer.ScrollY}");
        }

        Console.Error.WriteLine($"SELFTEST: searchBarVisible={_searchBar.Visible}");

        // Walk every match and report the scroll offset, which proves
        // cross-page navigation actually moves the view.
        var before = _viewer.ScrollY;
        for (var i = 0; i < Math.Min(4, _viewer.MatchCount); i++)
        {
            _viewer.GoToNextMatch();
            Console.Error.WriteLine($"SELFTEST: nav active={_viewer.ActiveMatchNumber}/{_viewer.MatchCount} scroll={_viewer.ScrollY}");
        }
        Console.Error.WriteLine($"SELFTEST: scroll moved from {before} to {_viewer.ScrollY}");
    }

    /// <summary>
    /// Drives the viewer through the operations that change render generations:
    /// deep scrolls and repeated zoom changes. Used by <c>--stress --diag</c> to
    /// prove that memory returns to a steady state instead of climbing.
    /// </summary>
    private void RunStressPass()
    {
        if (_document is null)
        {
            return;
        }

        for (var round = 0; round < 3; round++)
        {
            for (var i = 1; i <= 8; i++)
            {
                var target = (int)(_viewer.ContentHeight * i / 9.0);
                _viewer.ScrollTo(target);
                Application.DoEvents();
                Thread.Sleep(120);
            }

            ApplyZoom(_viewer.Zoom * 1.25f);
            Application.DoEvents();
            Thread.Sleep(200);
            ApplyZoom(_viewer.Zoom / 1.25f);
            Application.DoEvents();
            Thread.Sleep(200);
        }

        _viewer.ScrollTo(0);
        Application.DoEvents();
        Thread.Sleep(600);
    }

    private void BuildLayout()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Canvas,
            Padding = new Padding(0),
        };

        _scrollBar.Dock = DockStyle.Right;
        _scrollBar.Width = 12;

        _viewer.Dock = DockStyle.Fill;

        // WinForms lays docked children out from the last one added, so Fill
        // goes in first and the edges after it. The old order gave the viewer
        // the whole window: its client area measured the full form, the
        // scrollbar painted on top of the page and the document opened partly
        // hidden under the chrome. The same rule orders the top chrome, so the
        // title strip is added last and lands on the very top.
        _sidePanel.Dock = DockStyle.Left;
        _sidePanel.Width = 190;
        _sidePanel.Visible = false;

        host.Controls.Add(_viewer);
        host.Controls.Add(_scrollBar);
        host.Controls.Add(_sidePanel);

        Controls.Add(host);
        Controls.Add(_statusBar);
        Controls.Add(_searchBar);
        Controls.Add(_toolbar);
        Controls.Add(_titleBar);
    }

    /// <summary>
    /// Attaches the shared themed tooltips. Only icon-only controls and hidden
    /// shortcuts get one; the shortcut, when there is one, rides on the same
    /// line as a chip.
    /// </summary>
    private void BuildToolTips()
    {
        _toolTip = new ThemedToolTip();

        _toolTip.Attach(_openButton, () => Strings.Get("Open"), () => "Ctrl+O");
        _toolTip.Attach(_zoomOutButton, () => Strings.Get("ZoomOut"), () => "Ctrl+-");
        _toolTip.Attach(_zoomInButton, () => Strings.Get("ZoomIn"), () => "Ctrl++");
        _toolTip.Attach(_fitWidthButton, () => Strings.Get("FitWidth"), () => "Alt+W");
        _toolTip.Attach(_rotateLeftButton, () => Strings.Get("RotateLeft"), () => "Alt+R");
        _toolTip.Attach(_rotateRightButton, () => Strings.Get("RotateRight"), () => "Ctrl+R");
        _toolTip.Attach(_copyButton, () => Strings.Get("Copy"), () => "Ctrl+C");
        _toolTip.Attach(_searchButton, () => Strings.Get("Search"), () => "Ctrl+F");
        _toolTip.Attach(_sidePanelButton, () => Strings.Get("ToggleSidePanel"), () => "F4");
        _toolTip.Attach(_themeButton, () => Strings.Get("ChangeTheme"), () => "Alt+T");
        _toolTip.Attach(_minimizeButton, () => Strings.Get("Minimize"), null);
        _toolTip.Attach(_maximizeButton,
            () => WindowState == FormWindowState.Maximized ? Strings.Get("Restore") : Strings.Get("Maximize"),
            () => "F11");
        _toolTip.Attach(_closeButton, () => Strings.Get("CloseWindow"), () => "Ctrl+W");
        _toolTip.Attach(_searchPreviousButton, () => Strings.Get("SearchPrevious"), () => "Shift+F3");
        _toolTip.Attach(_searchNextButton, () => Strings.Get("SearchNext"), () => "F3");
        _toolTip.Attach(_searchCloseButton, () => Strings.Get("SearchClear"), () => "Esc");
    }

    private void WireEvents()
    {
        _scrollBar.ValueChanged += (_, _) =>
        {
            if (_updatingScrollBar)
            {
                return;
            }
            _viewer.ScrollTo(_scrollBar.Value);
        };

        _viewer.ViewStateChanged += (_, _) =>
        {
            SyncScrollBar();
            UpdateStatus();
        };

        _viewer.SelectionChanged += (_, _) => UpdateSelectionState();
        _viewer.ViewStateChanged += (_, _) =>
        {
            if (_sidePanel.Visible)
            {
                _sidePanel.SetCurrentPage(_viewer.FirstVisiblePage);
                _sidePanel.EnsureAround(_viewer.FirstVisiblePage);
            }
        };
        _sidePanel.PageRequested += (_, page) => _viewer.ScrollToPage(page);
        _sidePanel.ModeChanged += (_, _) =>
        {
            _settings.SidePanelTab = _sidePanel.Mode;
            if (_documentPath is not null)
            {
                var key = NormalizePath(_documentPath);
                if (_settings.ViewStates.TryGetValue(key, out var state))
                {
                    state.SidePanelTab = _sidePanel.Mode;
                }
            }
            _store.Save(_settings);
        };
        _sidePanel.ResizeRequested += (_, width) => ResizeSidePanel(width);
        _sidePanel.ResizeCompleted += (_, _) => _store.Save(_settings);
        _viewer.SearchChanged += (_, _) => UpdateSearchState();
        _viewer.MouseUp += ViewerMouseUpForContextMenu;

        _viewer.MouseWheel += (_, _) => SyncScrollBar();
        _viewer.DragEnter += ViewerDragEnter;
        _viewer.DragDrop += ViewerDragDrop;
        DragEnter += ViewerDragEnter;
        DragDrop += ViewerDragDrop;

        KeyDown += OnWindowKeyDown;
    }

    private void SyncScrollBar()
    {
        _updatingScrollBar = true;
        try
        {
            _scrollBar.Maximum = Math.Max(0, _viewer.ContentHeight);
            _scrollBar.LargeChange = Math.Max(1, _viewer.ViewportHeight);
            _scrollBar.Value = _viewer.ScrollY;
        }
        finally
        {
            _updatingScrollBar = false;
        }
    }

    // --- Commands -----------------------------------------------------------

    private void OpenFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = Strings.Get("OpenFile"),
            Filter = "PDF (*.pdf)|*.pdf|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            OpenDocument(dialog.FileName);
        }
    }

    public void OpenDocument(string path)
    {
        UpdateStatus(Strings.Get("Loading"));
        _engine.OpenDocument(path, null, result =>
        {
            if (IsDisposed)
            {
                return;
            }
            BeginInvoke(() => OnDocumentOpened(path, result));
        });
    }

    private void OnDocumentOpened(string path, PdfOpenResult result)
    {
        if (!result.Success)
        {
            ShowOpenError(result.Error, result.PdfiumError);
            return;
        }

        var previous = _document;
        _document = result.Document;
        path = NormalizePath(path);
        _documentPath = path;

        _viewer.SetDocument(_document);
        _sidePanel.SetDocument(null);
        _sidePanel.Width = _settings.SidePanelWidth;

        // First view: fit the page width, but never zoom past 100%. A document
        // whose page is taller than the screen (an exported web page, a poster)
        // would otherwise open mid-page with the top cut off.
        FitWidthOnOpen();

        if (previous is not null)
        {
            _engine.CloseDocument(previous);
        }

        Text = $"{Path.GetFileName(path)} — {Strings.Get("AppTitle")}";
        InvalidateTitleBar();
        RememberRecent(path);
        ApplyStoredViewState(path);

        // After the stored zoom, so restoring the panel does not fight it. The
        // document's own choice wins; its tab comes back with it. A document
        // opened for the first time follows the global preference.
        var stored = _settings.RememberPagePosition
                     && _settings.ViewStates.TryGetValue(path, out var state)
            ? state
            : null;
        var panelOpen = stored?.SidePanelOpen ?? _settings.SidePanelOpen;
        if (panelOpen)
        {
            if (stored?.SidePanelTab is int tab)
            {
                _settings.SidePanelTab = tab;
            }
            SetSidePanelVisible(true, persist: false);
        }
        else
        {
            _sidePanel.Visible = false;
        }

        SyncScrollBar();
        UpdateCommandStates();
        UpdateStatus();
        _viewer.Focus();
    }

    /// <summary>Fits the page width on the first view, clamped to a 100% maximum.</summary>
    /// <summary>
    /// Zoom that fits a page's width inside the viewport. ClientSize is in
    /// device pixels while page sizes are in points, so the viewport is
    /// converted to 96 DPI units first: mixing the two over-zooms by the DPI
    /// factor on every monitor that is not at 100%.
    /// </summary>
    private float FitWidthZoomFor(int pageIndex)
    {
        if (_document is null)
        {
            return 1f;
        }
        var size = _document.GetPageSize(pageIndex);
        var scale = _viewer.DeviceDpi / 96f;
        if (scale <= 0)
        {
            scale = 1f;
        }
        // A proportional gutter reads better than a fixed one: at 150% zoom the
        // page should not run into the window edges.
        var viewportPixels = _viewer.ClientSize.Width;
        if (viewportPixels <= 0 || size.Width <= 0)
        {
            return 1f;
        }
        var gutter = Math.Max(32, (int)(viewportPixels * 0.06));
        var available = viewportPixels - gutter * 2;
        if (available <= 0)
        {
            return 1f;
        }
        return available / (size.Width * scale);
    }

    private void FitWidthOnOpen()
    {
        if (_document is null)
        {
            return;
        }
        _zoom = Math.Clamp(Math.Min(FitWidthZoomFor(0), 1f), 0.1f, 8f);
        _userZoomed = false;
        _viewer.SetZoom(_zoom, keepCenter: false);
        _viewer.ScrollTo(0);
    }

    /// <summary>
    /// Recomputes fit-width without touching the scroll offset or the stored
    /// position. Used when the panel changes the client width on an already
    /// open document, where scrolling back to the top would lose the reader.
    /// </summary>
    private void RefitWidth()
    {
        if (_document is null)
        {
            return;
        }
        var previous = _viewer.ScrollY;
        var page = _viewer.FirstVisiblePage;
        _zoom = Math.Clamp(Math.Min(FitWidthZoomFor(page), 1f), 0.1f, 8f);
        _viewer.SetZoom(_zoom, keepCenter: false);
        _viewer.RefreshLayout();

        // RebuildLayout keeps the visible page but not the pixel offset inside
        // it, so the scroll is restored against the new slot top.
        _viewer.ScrollToPage(page);
        _viewer.ScrollTo(_viewer.LayoutSlots.Count > page
            ? _viewer.ScrollY + (previous - _viewer.LayoutSlots[page].Top)
            : previous);
    }

    /// <summary>
    /// Canonical form of a document path, used as the settings key and for
    /// recent files. Without it, an unnormalized path and a normalized one are
    /// two separate entries for the same document.
    /// </summary>
    private static string NormalizePath(string incoming)
    {
        try
        {
            return Path.GetFullPath(incoming);
        }
        catch (ArgumentException)
        {
            return incoming;
        }
    }

    private void ApplyStoredViewState(string path)
    {
        var key = NormalizePath(path);
        if (!_settings.RememberPagePosition || !_settings.ViewStates.TryGetValue(key, out var state))
        {
            // No stored position: keep the fit-width view computed on open and
            // only make sure the rotation starts neutral.
            _rotation = 0;
            _viewer.SetRotation(0);
            _viewer.ScrollTo(0);
            return;
        }

        // The stored zoom is a logical factor, but it was chosen against a
        // viewport measured at the DPI of that session. Restoring it verbatim on
        // a different DPI can open the page wider than the window, which reads as
        // a broken document. Fit width is therefore the ceiling: a stored zoom
        // that no longer fits is replaced by the fitting one.
        var fitZoom = FitWidthZoomFor(0);
        var ceiling = Math.Clamp(Math.Min(fitZoom, 1f), 0.1f, 8f);
        _zoom = state.Zoom <= 0 || state.Zoom > ceiling ? ceiling : state.Zoom;
        _rotation = state.Rotation;
        _viewer.SetZoom(_zoom, keepCenter: false);
        _viewer.SetRotation(_rotation);
        _viewer.ScrollTo(0);
        _viewer.ScrollToPage(state.Page);
        SyncScrollBar();
    }

    private void ShowOpenError(PdfOpenError error, int code)
    {
        var key = error switch
        {
            PdfOpenError.NotFound => "ErrorNotFound",
            PdfOpenError.AccessDenied => "ErrorAccess",
            PdfOpenError.InvalidFormat => "ErrorCorrupt",
            PdfOpenError.PasswordRequired => "ErrorPassword",
            PdfOpenError.WrongPassword => "WrongPassword",
            PdfOpenError.Unsupported => "ErrorUnsupported",
            _ => null,
        };

        var message = key is null ? Strings.Format("ErrorUnknown", code) : Strings.Get(key);
        MessageBox.Show(this, message, Strings.Get("ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        UpdateStatus();
    }

    /// <summary>Shows the search bar and focuses the field.</summary>
    private void OpenSearch()
    {
        if (_document is null)
        {
            return;
        }

        _searchBar.Visible = true;
        _searchField.Focus();
        _searchField.SelectAll();
        RescaleChrome();
    }

    /// <summary>Shows or hides the thumbnails and bookmarks panel.</summary>
    private void ToggleSidePanel()
    {
        if (_document is null)
        {
            return;
        }

        SetSidePanelVisible(!_sidePanel.Visible, persist: true);
    }

    /// <summary>
    /// Opens or closes the panel and remembers the choice. The same path is used
    /// when a document opens, so a panel left open comes back without the user
    /// asking twice.
    /// </summary>
    private void SetSidePanelVisible(bool visible, bool persist)
    {
        if (_document is null)
        {
            return;
        }

        _sidePanel.Visible = visible;
        if (visible)
        {
            _sidePanel.SetDocument(_document);
            _sidePanel.SetTab(_settings.SidePanelTab);
            _sidePanel.SetCurrentPage(_viewer.FirstVisiblePage);
            _sidePanel.EnsureAround(_viewer.FirstVisiblePage);
            _sidePanel.LoadBookmarks();
        }

        // The panel narrows the viewer. Without a refit a page wider than the new
        // client area stays anchored where it was and ends up clipped on the
        // right, so the width is refitted here. The scroll position survives:
        // toggling the panel must not send the reader back to the first page.
        if (!_userZoomed)
        {
            RefitWidth();
        }
        else
        {
            _viewer.RefreshLayout();
        }

        if (persist)
        {
            // Remembered globally for the next document that has no entry of its
            // own, and per document so this file keeps its own choice.
            _settings.SidePanelOpen = visible;
            if (_documentPath is not null)
            {
                var key = NormalizePath(_documentPath);
                if (!_settings.ViewStates.TryGetValue(key, out var state))
                {
                    state = new DocumentViewState();
                    _settings.ViewStates[key] = state;
                }
                state.SidePanelOpen = visible;
                state.SidePanelTab = _sidePanel.Mode;
            }
            _store.Save(_settings);
        }
        _viewer.Focus();
    }

    /// <summary>
    /// Applies a width dragged on the panel edge, clamped so the document keeps a
    /// usable sliver of the window.
    /// </summary>
    private void ResizeSidePanel(int width)
    {
        var scale = DeviceDpi / 96f;
        var minimum = (int)Math.Round(AppSettings.MinSidePanelWidth * scale);
        var maximum = (int)Math.Round(AppSettings.MaxSidePanelWidth * scale);
        var windowLimit = Math.Max(minimum, ClientSize.Width - (int)Math.Round(320 * scale));
        var clamped = Math.Clamp(width, minimum, Math.Min(maximum, windowLimit));
        if (clamped == _sidePanel.Width)
        {
            return;
        }

        _sidePanel.Width = clamped;
        if (!_userZoomed)
        {
            RefitWidth();
        }
        else
        {
            _viewer.RefreshLayout();
        }

        _settings.SidePanelWidth = clamped;
    }

    private void CloseSearch()
    {
        _searchBar.Visible = false;
        _viewer.ClearSearch();
        _searchResults.Text = string.Empty;
        _viewer.Focus();
        RescaleChrome();
    }

    /// <summary>
    /// Runs the current query. The viewer reports progress as pages are
    /// extracted, so the counter updates while typing instead of freezing.
    /// </summary>
    private void RunSearch()
    {
        _viewer.Search(_searchField.Text);
    }

    private void OnSearchFieldKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter:
                if (e.Shift)
                {
                    _viewer.GoToPreviousMatch();
                }
                else
                {
                    _viewer.GoToNextMatch();
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
            case Keys.Escape:
                CloseSearch();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
        }
    }

    /// <summary>
    /// Enables the copy command and refreshes the status hint that tells the user
    /// what the current selection is worth.
    /// </summary>
    /// <summary>
    /// Reflects the current search in the bar: match count, position, and whether
    /// the navigation buttons can do anything.
    /// </summary>
    private void UpdateSearchState()
    {
        var term = _viewer.SearchTerm;
        if (string.IsNullOrEmpty(term))
        {
            _searchResults.Text = string.Empty;
            _searchPreviousButton.Enabled = false;
            _searchNextButton.Enabled = false;
            return;
        }

        var total = _viewer.MatchCount;
        _searchResults.Text = total == 0
            ? Strings.Get("SearchNoResults")
            : Strings.Format("SearchResults", _viewer.ActiveMatchNumber, total);
        _searchPreviousButton.Enabled = total > 0;
        _searchNextButton.Enabled = total > 0;
    }

    private void UpdateSelectionState()
    {
        _copyButton.Enabled = _viewer.HasSelection;
        UpdateStatus();
    }

    /// <summary>Copies the selected text to the clipboard.</summary>
    private void CopySelection()
    {
        var text = _viewer.GetSelectedText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            UpdateStatus(Strings.Format("CopiedCharacters", text.Length));
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // The clipboard is a shared OS resource and another process can hold
            // it. Losing one copy is not worth an error dialog.
            Core.Diagnostics.AppLog.Warn("Clipboard was busy; the copy was skipped.");
        }
    }

    /// <summary>
    /// Shows the page context menu. A reader expects Copy and Select all here,
    /// themed like the rest of the app instead of the Windows menu.
    /// </summary>
    private void ShowPageContextMenu(Point screenLocation)
    {
        if (_document is null)
        {
            return;
        }

        var menu = ThemedContextMenu.Create(
            new ContextMenuItem(Strings.Get("Copy"), CopySelection, IconKind.None, _viewer.HasSelection),
            new ContextMenuItem(Strings.Get("SelectAll"), () => _viewer.SelectAllOnCurrentPage(), IconKind.None, true));
        menu.Show(screenLocation);
    }

    private void ViewerMouseUpForContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && _document is not null)
        {
            ShowPageContextMenu(Cursor.Position);
        }
    }

    private void ChangeZoom(float factor)
    {
        _userZoomed = true;
        ApplyZoom(_viewer.Zoom * factor);
    }

    private void ApplyZoom(float zoom, bool userInitiated = true)
    {
        if (userInitiated)
        {
            _userZoomed = true;
        }
        _zoom = Math.Clamp(zoom, 0.1f, 8f);
        _viewer.SetZoom(_zoom);
        SyncScrollBar();
        UpdateStatus();
        SaveViewState();
    }

    private void FitWidth()
    {
        if (_document is null)
        {
            return;
        }
        ApplyZoom(FitWidthZoomFor(_viewer.FirstVisiblePage));
    }

    private void Rotate(int degrees)
    {
        _rotation = ((_rotation + degrees) % 360 + 360) % 360;
        _viewer.SetRotation(_rotation);
        SyncScrollBar();
        UpdateStatus();
        SaveViewState();
    }

    private void CycleTheme()
    {
        var next = Theme.CurrentTheme switch
        {
            AppTheme.Default => AppTheme.Gray,
            AppTheme.Gray => AppTheme.Light,
            _ => AppTheme.Default,
        };
        _settings.Theme = next;
        Theme.Apply(next);
        ApplyTheme();
        _store.Save(_settings);
    }

    private void ApplyTheme()
    {
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        _titleBar.BackColor = Theme.Bg;
        _toolbar.BackColor = Theme.Surface;
        _statusBar.BackColor = Theme.Surface;
        _pageLabel.ForeColor = Theme.TextMuted;
        _zoomLabel.ForeColor = Theme.TextMuted;
        _titleLabel.ForeColor = Theme.Text;
        _searchBar.BackColor = Theme.Surface;
        _searchField.BackColor = Theme.Canvas;
        _searchField.ForeColor = Theme.Text;
        _searchResults.ForeColor = Theme.TextMuted;
        _themeButton.Icon = Theme.CurrentTheme switch
        {
            AppTheme.Gray => IconKind.ThemeGray,
            AppTheme.Light => IconKind.ThemeLight,
            _ => IconKind.ThemeDefault,
        };
        _themeButton.AccessibleName = Strings.Get("Settings");

        foreach (Control control in _toolbar.Controls)
        {
            if (control is FlowLayoutPanel flow)
            {
                flow.BackColor = Color.Transparent;
                foreach (Control child in flow.Controls)
                {
                    if (child is Panel separator)
                    {
                        separator.BackColor = Theme.Border;
                    }
                    child.Invalidate();
                }
            }
        }

        _viewer.Invalidate();
        _scrollBar.Invalidate();
        _titleBar.Invalidate();
        _toolbar.Invalidate();
        _statusBar.Invalidate();
    }

    // --- Status -------------------------------------------------------------

    private void UpdateStatus(string? overrideText = null)
    {
        if (overrideText is not null)
        {
            _pageLabel.Text = overrideText;
            return;
        }

        if (_document is null)
        {
            _pageLabel.Text = Strings.Get("NoDocument");
            _zoomLabel.Text = string.Empty;
            return;
        }

        var page = Math.Min(_viewer.FirstVisiblePage + 1, _document.PageCount);
        _pageLabel.Text = Strings.Format("PageOf", page, _document.PageCount);
        _zoomLabel.Text = Strings.Format("ZoomPercent", (int)Math.Round(_viewer.Zoom * 100));
        InvalidateTitleBar();
    }

    private void UpdateCommandStates()
    {
        var hasDocument = _document is not null;
        _zoomInButton.Enabled = hasDocument;
        _zoomOutButton.Enabled = hasDocument;
        _fitWidthButton.Enabled = hasDocument;
        _rotateLeftButton.Enabled = hasDocument;
        _rotateRightButton.Enabled = hasDocument;
        _copyButton.Enabled = hasDocument && _viewer.HasSelection;
        _searchButton.Enabled = hasDocument;
        _sidePanelButton.Enabled = hasDocument;
        if (!hasDocument && _sidePanel.Visible)
        {
            _sidePanel.Visible = false;
        }
        if (!hasDocument && _searchBar.Visible)
        {
            CloseSearch();
        }
    }

    // --- Persistence --------------------------------------------------------

    private void RememberRecent(string path)
    {
        path = NormalizePath(path);
        _settings.RecentFiles.RemoveAll(p => string.Equals(NormalizePath(p), path, StringComparison.OrdinalIgnoreCase));
        _settings.RecentFiles.Insert(0, path);
        if (_settings.RecentFiles.Count > AppSettings.MaxRecentFiles)
        {
            _settings.RecentFiles.RemoveRange(AppSettings.MaxRecentFiles, _settings.RecentFiles.Count - AppSettings.MaxRecentFiles);
        }
        _store.Save(_settings);
    }

    private void SaveViewState()
    {
        if (_documentPath is null || !_settings.RememberPagePosition)
        {
            return;
        }
        _settings.ViewStates[NormalizePath(_documentPath)] = new DocumentViewState
        {
            Page = _viewer.FirstVisiblePage,
            Zoom = _viewer.Zoom,
            Rotation = _viewer.Rotation,
            SidePanelOpen = _sidePanel.Visible,
            SidePanelTab = _sidePanel.Mode,
        };
        _store.Save(_settings);
    }

    // --- Window chrome ------------------------------------------------------

    private const int WmNcLeftButtonDown = 0x00A1;
    private const int WmEnterSizeMove = 0x0231;
    private const int WmExitSizeMove = 0x0232;
    private const int HtCaption = 2;
    private int _moveLoopEntries;
    private int _moveLoopExits;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extraInfo);

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    /// <summary>
    /// Hands the drag to Windows. Moving the form from a MouseMove handler does
    /// not work here: the label covering the title strip receives the mouse and
    /// the form never sees the movement. The native path also brings Snap and
    /// drag-to-the-top maximize for free.
    /// </summary>
    private void TitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        ReleaseCapture();
        SendMessage(Handle, WmNcLeftButtonDown, HtCaption, IntPtr.Zero);
    }

    /// <summary>
    /// The wheel follows the focus in WinForms, and after using a toolbar button
    /// the focus sits on that button, not on the page. A reader should scroll
    /// from anywhere in the window, so the window forwards whatever the viewer
    /// did not already handle.
    /// </summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e is HandledMouseEventArgs { Handled: true } || _document is null)
        {
            return;
        }
        _viewer.ScrollByWheel(e.Delta);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    /// <summary>
    /// Updates the window title strip. The document name replaces the app name
    /// once a file is open, so the strip always answers "what am I reading".
    /// </summary>
    private void InvalidateTitleBar()
    {
        var appName = Strings.Get("AppTitle");
        if (_documentPath is null)
        {
            _titleLabel.Text = $"{appName} · {Strings.Get("AppSubtitle")}";
            return;
        }
        _titleLabel.Text = $"{Path.GetFileName(_documentPath)} · {appName}";
    }

    // --- Drag and drop, shortcuts, lifetime ---------------------------------

    private void ViewerDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            e.Effect = files?.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }
    }

    private void ViewerDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
        {
            return;
        }
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        var pdf = files?.FirstOrDefault(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is not null)
        {
            OpenDocument(pdf);
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.O when e.Control:
                OpenFile();
                e.Handled = true;
                break;
            case Keys.Oemplus or Keys.Add when e.Control:
                ChangeZoom(1.25f);
                e.Handled = true;
                break;
            case Keys.OemMinus or Keys.Subtract when e.Control:
                ChangeZoom(1 / 1.25f);
                e.Handled = true;
                break;
            case Keys.D0 when e.Control:
                ApplyZoom(1f);
                e.Handled = true;
                break;
            case Keys.R when e.Control:
                Rotate(90);
                e.Handled = true;
                break;
            case Keys.R when e.Alt:
                Rotate(-90);
                e.Handled = true;
                break;
            case Keys.W when e.Alt:
                FitWidth();
                e.Handled = true;
                break;
            case Keys.T when e.Alt:
                CycleTheme();
                e.Handled = true;
                break;
            case Keys.C when e.Control:
                CopySelection();
                e.Handled = true;
                break;
            case Keys.A when e.Control:
                _viewer.SelectAllOnCurrentPage();
                e.Handled = true;
                break;
            case Keys.F when e.Control:
                OpenSearch();
                e.Handled = true;
                break;
            case Keys.F4:
                ToggleSidePanel();
                e.Handled = true;
                break;
            case Keys.F3 when e.Shift:
                _viewer.GoToPreviousMatch();
                e.Handled = true;
                break;
            case Keys.F3:
                _viewer.GoToNextMatch();
                e.Handled = true;
                break;
            case Keys.W when e.Control:
                Close();
                e.Handled = true;
                break;
            case Keys.Escape when WindowState == FormWindowState.Maximized:
                WindowState = FormWindowState.Normal;
                e.Handled = true;
                break;
            case Keys.F11:
                ToggleMaximize();
                e.Handled = true;
                break;
        }
    }

    private bool _userZoomed;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_maximizeButton.Icon != IconKind.None)
        {
            _maximizeButton.Icon = WindowState == FormWindowState.Maximized ? IconKind.Restore : IconKind.Maximize;
        }
        // Keep the page fitted to the window while the user has not chosen a zoom
        // level explicitly, which is what every reader does on resize.
        if (!_userZoomed && _document is not null && WindowState != FormWindowState.Minimized)
        {
            FitWidthOnOpen();
            SyncScrollBar();
            UpdateStatus();
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        RescaleChrome();

        // WinForms has already applied DPI scaling here, so this is the one place
        // where setting a size in device pixels sticks. The design size is capped
        // to the monitor's working area: a 1100x820 layout at 150% would
        // otherwise open taller than a 1080p screen.
        var area = Screen.FromControl(this).WorkingArea;
        var targetWidth = (int)Math.Round(1100 * DeviceDpi / 96f);
        var targetHeight = (int)Math.Round(820 * DeviceDpi / 96f);
        var width = Math.Min(targetWidth, Math.Max(560, area.Width - 40));
        var height = Math.Min(targetHeight, Math.Max(420, area.Height - 40));

        Bounds = new Rectangle(
            area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2,
            width,
            height);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ScheduleDiagnosticsDump();
    }

    /// <summary>
    /// Sizes the toolbar and the scrollbar for the monitor's effective DPI.
    /// Runs in OnLoad because sizes computed in the constructor are measured
    /// before WinForms applies scaling, which clips localized captions.
    /// </summary>
    private void RescaleChrome()
    {
        var scale = DeviceDpi / 96f;
        _scrollBar.Width = Math.Max(8, (int)Math.Round(10 * scale));
        _titleBar.Height = (int)Math.Round(TitleBarHeight * scale);
        _toolbar.Height = (int)Math.Round(ToolbarHeight * scale);
        _statusBar.Height = (int)Math.Round(StatusBarHeight * scale);
        _searchBar.Height = (int)Math.Round(40 * scale);
        _searchField.Width = (int)Math.Round(260 * scale);
        _searchResults.Width = (int)Math.Round(110 * scale);
        _pageLabel.Width = (int)Math.Round(260 * scale);

        var titleHeight = (int)Math.Round(TitleBarHeight * scale);
        foreach (var windowButton in new[] { _themeButton, _minimizeButton, _maximizeButton, _closeButton })
        {
            windowButton.Size = new Size((int)Math.Round(46 * scale), titleHeight);
        }
        _maximizeButton.Icon = WindowState == FormWindowState.Maximized ? IconKind.Restore : IconKind.Maximize;

        foreach (Control control in _toolbar.Controls)
        {
            if (control is not FlowLayoutPanel flow)
            {
                continue;
            }
            flow.Padding = new Padding((int)(8 * scale), (int)(6 * scale), (int)(8 * scale), (int)(6 * scale));
            foreach (Control child in flow.Controls)
            {
                if (child is ToolButton button)
                {
                    button.Size = string.IsNullOrEmpty(button.Text)
                        ? new Size((int)Math.Round(34 * scale), (int)Math.Round(32 * scale))
                        : button.MeasurePreferredSize(scale);
                }
                else if (child is Panel separator)
                {
                    separator.Size = new Size(1, (int)Math.Round(22 * scale));
                    separator.Margin = new Padding((int)(6 * scale), (int)(3 * scale), (int)(6 * scale), (int)(3 * scale));
                }
            }
        }
    }

    /// <summary>
    /// Receives the inter-process handoff used by the single-instance rule: a
    /// second launch sends WM_COPYDATA with the document to open.
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == SingleInstance.WmCopyData && _singleInstance?.TryHandleCopyData(ref m) == true)
        {
            Activate();
            return;
        }
        if (m.Msg == WmEnterSizeMove)
        {
            _moveLoopEntries++;
        }
        else if (m.Msg == WmExitSizeMove)
        {
            _moveLoopExits++;
        }
        base.WndProc(ref m);
    }

    private SingleInstance? _singleInstance;

    /// <summary>Runs a search without opening the bar, for screenshot verification.</summary>
    internal void SelfTestSearch(string term)
    {
        OpenSearch();
        _searchField.Text = term;
        RunSearch();
    }

    /// <summary>Matches found by the last self-test search.</summary>
    internal int SelfTestMatchCount => _viewer.MatchCount;

    /// <summary>Shows the side panel on a tab, for screenshot verification.</summary>
    internal void ShowSidePanel(int tab)
    {
        if (_document is null)
        {
            return;
        }
        if (!_sidePanel.Visible)
        {
            ToggleSidePanel();
        }
        _sidePanel.SetTab(tab);
    }

    /// <summary>
    /// Rows the panel has ready: miniatures on the pages tab, outline entries on
    /// the bookmarks tab. A capture waits on whichever tab is open, so a search
    /// screenshot never records an empty list that was still loading.
    /// </summary>
    internal int SidePanelReadyCount => _sidePanel.Mode == 0
        ? _sidePanel.ThumbnailCount
        : _sidePanel.BookmarkCount;

    /// <summary>Attaches the single-instance helper so WndProc can route to it.</summary>
    public void AttachSingleInstance(SingleInstance singleInstance)
    {
        _singleInstance = singleInstance;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveViewState();
        _toolTip?.Dispose();
        base.OnFormClosing(e);
    }

    /// <summary>Writes the window size and location for the next session.</summary>
    public void PersistWindowBounds()
    {
        if (WindowState == FormWindowState.Normal)
        {
            _settings.ViewStates["__window"] = new DocumentViewState();
        }
        _store.Save(_settings);
    }
}
