using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;
using CyberPDF.Core.Search;
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
    private readonly AnnotationStore _annotationStore = new();
    private readonly AnnotationSet _annotations;
    private DocumentAnnotations _currentAnnotations = new();

    private readonly Panel _titleBar = new();
    private readonly Panel _brandButton = new();
    private bool _brandHovered;
    private readonly TabStrip _tabStrip = new();
    private readonly Panel _toolbar = new();
    private readonly Panel _statusBar = new();
    private readonly Panel _contentHost = new();
    private readonly PageViewer _viewer;
    private readonly ThinScrollBar _scrollBar = new();
    private readonly ThinScrollBar _hScrollBar = new() { Orientation = ScrollBarOrientation.Horizontal };
    private readonly ToolButton _prevPageButton = new();
    private readonly ToolButton _nextPageButton = new();
    private readonly Label _pageLabel = new();
    private readonly UndoableTextBox _pageJumpField = new();
    private readonly Label _zoomLabel = new();
    private readonly Label _titleLabel = new();
    private readonly ToolButton _openButton = new();
    private readonly ToolButton _zoomOutButton = new();
    private readonly ToolButton _zoomInButton = new();
    private readonly ToolButton _fitWidthButton = new();
    private readonly ToolButton _fitPageButton = new();
    private readonly ToolButton _viewModeButton = new();
    private readonly ToolButton _darkPageButton = new();
    private readonly ToolButton _rotateLeftButton = new();
    private readonly ToolButton _rotateRightButton = new();
    private readonly ToolButton _handToolButton = new();
    private readonly ToolButton _themeButton = new();
    private readonly ToolButton _copyButton = new();
    private readonly ToolButton _highlightButton = new();
    private readonly ToolButton _bookmarkButton = new();
    private readonly ToolButton _searchButton = new();
    private readonly ToolButton _printButton = new();
    private readonly ToolButton _settingsButton = new();
    private readonly Panel _searchBar = new();
    private readonly Panel _searchBoxHost = new();
    private readonly UndoableTextBox _searchField = new();
    private readonly ToolButton _matchCaseButton = new();
    private readonly ToolButton _wholeWordButton = new();
    private readonly Label _searchResults = new();
    private readonly ToolButton _searchPreviousButton = new();
    private readonly ToolButton _searchNextButton = new();
    private readonly ToolButton _searchCloseButton = new();
    private readonly ToolButton _minimizeButton = new();
    private readonly ToolButton _maximizeButton = new();
    private readonly ToolButton _closeButton = new();
    private readonly ToolButton _sidePanelButton = new();
    private readonly SidePanel _sidePanel;
    private readonly WelcomeView _welcomeView;
    private readonly Image? _appIconBitmap;
    private ResizeBorderFilter? _resizeFilter;
    private ThemedToolTip? _toolTip;
    private TrayController? _trayController;

    private readonly List<DocumentSession> _sessions = new();
    private int _activeSessionIndex = -1;
    private bool _showingStartTab;
    private PdfDocument? _document;
    private bool _diagRequested;
    private bool _stressRequested;
    private bool _suppressPersistenceOnClose;
    private string? _documentPath;
    private float _zoom = 1f;
    private int _rotation;
    private bool _updatingScrollBar;
    private bool _fitPageActive;
    private bool _forceExit;
    private bool _closePromptOpen;
    private string? _linkHoverPreview;

    public MainWindow(PdfEngine engine, AppSettings settings, SettingsStore store)
    {
        _engine = engine;
        _settings = settings;
        _store = store;
        _annotations = _annotationStore.Load();
        _viewer = new PageViewer(engine);
        _sidePanel = new SidePanel(engine);

        LoadEmbeddedIcons(out var windowIcon, out _appIconBitmap);
        if (windowIcon is not null)
        {
            Icon = windowIcon;
        }
        _welcomeView = new WelcomeView(_appIconBitmap);
        _welcomeView.SetRecentFiles(_settings.RecentFiles);

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
        Padding = new Padding(1);

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
        _titleBar.Resize += (_, _) => PositionTitleLabel();

        _brandButton.Dock = DockStyle.Left;
        _brandButton.Width = 104;
        _brandButton.BackColor = Color.Transparent;
        _brandButton.Cursor = Cursors.Hand;
        _brandButton.AccessibleName = Strings.Get("AboutTitle");
        _brandButton.MouseEnter += (_, _) => { _brandHovered = true; _brandButton.Invalidate(); };
        _brandButton.MouseLeave += (_, _) => { _brandHovered = false; _brandButton.Invalidate(); };
        _brandButton.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowAboutDialog();
            }
        };
        _brandButton.Paint += (_, e) =>
        {
            var scale = DeviceDpi / 96f;
            var iconSize = Math.Max(16, (int)Math.Round(18 * scale));
            var padX = Math.Max(6, (int)Math.Round(10 * scale));
            var gap = Math.Max(4, (int)Math.Round(6 * scale));
            ThemePainter.PrepareHighQuality(e.Graphics);

            if (_brandHovered)
            {
                var pill = Rectangle.Inflate(_brandButton.ClientRectangle, -(int)Math.Round(4 * scale), -(int)Math.Round(4 * scale));
                ThemePainter.FillRounded(e.Graphics, pill, 6, Theme.Hover);
            }

            var x = padX;
            if (_appIconBitmap is not null)
            {
                var iy = (_brandButton.Height - iconSize) / 2;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                e.Graphics.DrawImage(_appIconBitmap, new Rectangle(x, iy, iconSize, iconSize));
                x += iconSize + gap;
            }

            using var brandFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var cyberSize = TextRenderer.MeasureText(e.Graphics, "Cyber", brandFont, new Size(int.MaxValue, int.MaxValue), flags);
            var textY = (_brandButton.Height - cyberSize.Height) / 2;
            TextRenderer.DrawText(e.Graphics, "Cyber", brandFont, new Point(x, textY), Theme.Text, flags);
            TextRenderer.DrawText(e.Graphics, "PDF", brandFont, new Point(x + cyberSize.Width, textY), Theme.Accent, flags);
        };

        _titleLabel.AutoSize = false;
        _titleLabel.Dock = DockStyle.None;
        _titleLabel.Anchor = AnchorStyles.None;
        _titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        _titleLabel.Padding = Padding.Empty;
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
        _closeButton.IsDanger = true;

        _titleBar.Controls.Add(_brandButton);
        // Right-docked, the control added last sits closest to the edge, so the
        // close button goes in last to keep the suite's minimize/maximize/close.
        _titleBar.Controls.Add(_themeButton);
        _titleBar.Controls.Add(_minimizeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_closeButton);
        _titleBar.Controls.Add(_titleLabel);
        PositionTitleLabel();
    }

    private static void ConfigureWindowButton(ToolButton button, IconKind icon, string accessibleName, Action onClick)
    {
        button.Icon = icon;
        button.Dock = DockStyle.Right;
        button.Width = 46;
        button.Height = TitleBarHeight;
        button.IsWindowChrome = true;
        button.CornerRadius = 0;
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
        ConfigureButton(_fitPageButton, IconKind.FitPage, null, FitPage);
        ConfigureButton(_viewModeButton, IconKind.ContinuousPage, null, ShowViewModeMenu);
        ConfigureButton(_darkPageButton, IconKind.InvertColors, null, ToggleDarkPageMode);
        ConfigureButton(_rotateLeftButton, IconKind.RotateLeft, null, () => Rotate(-90));
        ConfigureButton(_rotateRightButton, IconKind.RotateRight, null, () => Rotate(90));
        ConfigureButton(_handToolButton, IconKind.Hand, null, ToggleHandTool);
        ConfigureButton(_copyButton, IconKind.Copy, Strings.Get("Copy"), CopySelection);
        _copyButton.Enabled = false;
        ConfigureButton(_highlightButton, IconKind.Highlight, null, OnHighlightButtonClick);
        _highlightButton.AccessibleName = Strings.Get("Highlight");
        _highlightButton.IndicatorColor = Theme.HighlightColor(_settings.HighlightColor);
        _highlightButton.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && _document is not null && !_showingStartTab)
            {
                ShowHighlightColorMenu();
            }
        };
        _highlightButton.Enabled = false;
        ConfigureButton(_bookmarkButton, IconKind.Bookmark, null, ToggleCurrentPageBookmark);
        _bookmarkButton.AccessibleName = Strings.Get("ToggleBookmark");
        ConfigureButton(_searchButton, IconKind.Search, null, OpenSearch);
        ConfigureButton(_printButton, IconKind.Print, null, PrintDocument);
        ConfigureButton(_sidePanelButton, IconKind.SidePanel, null, ToggleSidePanel);

        _settingsButton.Icon = IconKind.Settings;
        _settingsButton.Dock = DockStyle.Right;
        _settingsButton.Width = 42;
        _settingsButton.AccessibleName = Strings.Get("Settings");
        _settingsButton.Click += (_, _) => ShowSettingsMenu();

        flow.Controls.Add(_openButton);
        flow.Controls.Add(_sidePanelButton);
        flow.Controls.Add(Separator());
        flow.Controls.Add(_zoomOutButton);
        flow.Controls.Add(_zoomInButton);
        flow.Controls.Add(_fitWidthButton);
        flow.Controls.Add(_fitPageButton);
        flow.Controls.Add(_viewModeButton);
        flow.Controls.Add(_darkPageButton);
        flow.Controls.Add(Separator());
        flow.Controls.Add(_rotateLeftButton);
        flow.Controls.Add(_rotateRightButton);
        flow.Controls.Add(_handToolButton);
        flow.Controls.Add(_copyButton);
        flow.Controls.Add(_highlightButton);
        flow.Controls.Add(_bookmarkButton);
        flow.Controls.Add(Separator());
        flow.Controls.Add(_searchButton);
        flow.Controls.Add(_printButton);

        _toolbar.Controls.Add(flow);
        _toolbar.Controls.Add(_settingsButton);
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
        _searchBar.Dock = DockStyle.None;
        _searchBar.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _searchBar.Size = new Size(444, 42);
        _searchBar.BackColor = Theme.Surface;
        _searchBar.Visible = false;
        _searchBar.SizeChanged += (_, _) => UpdateSearchPillRegion();
        _searchBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            ThemePainter.PrepareHighQuality(g);
            var rect = new Rectangle(0, 0, _searchBar.Width - 1, _searchBar.Height - 1);
            ThemePainter.FillRounded(g, rect, 8, Theme.Surface);
            ThemePainter.DrawRounded(g, rect, 8, Theme.BorderStrong);
        };

        // Rounded themed container around a borderless UndoableTextBox so the
        // search box matches the palette and inherits Undo/Redo + right-click menu.
        _searchBoxHost.Size = new Size(188, 28);
        _searchBoxHost.BackColor = Theme.Surface;
        _searchBoxHost.Margin = new Padding(0, 1, 4, 1);
        _searchBoxHost.Paint += (_, e) =>
        {
            var g = e.Graphics;
            ThemePainter.PrepareHighQuality(g);
            var rect = new Rectangle(0, 0, _searchBoxHost.Width - 1, _searchBoxHost.Height - 1);
            ThemePainter.FillRounded(g, rect, 6, Theme.Canvas);
            ThemePainter.DrawRounded(g, rect, 6, _searchField.Focused ? Theme.Accent : Theme.Border);
        };

        _searchField.BorderStyle = BorderStyle.None;
        _searchField.BackColor = Theme.Canvas;
        _searchField.ForeColor = Theme.Text;
        _searchField.Dock = DockStyle.Fill;
        _searchField.CueText = Strings.Get("SearchPlaceholder");
        _searchField.TextChanged += (_, _) => RunSearch();
        _searchField.KeyDown += OnSearchFieldKeyDown;
        _searchField.GotFocus += (_, _) => _searchBoxHost.Invalidate();
        _searchField.LostFocus += (_, _) => _searchBoxHost.Invalidate();
        _searchBoxHost.Padding = new Padding(8, 5, 8, 4);
        _searchBoxHost.Controls.Add(_searchField);

        ConfigureButton(_matchCaseButton, IconKind.None, "Aa", () =>
        {
            _matchCaseButton.IsActive = !_matchCaseButton.IsActive;
            RunSearch();
        });
        _matchCaseButton.AccessibleName = Strings.Get("MatchCase");

        ConfigureButton(_wholeWordButton, IconKind.None, "ab", () =>
        {
            _wholeWordButton.IsActive = !_wholeWordButton.IsActive;
            RunSearch();
        });
        _wholeWordButton.AccessibleName = Strings.Get("WholeWord");

        _searchResults.AutoSize = false;
        _searchResults.Width = 72;
        _searchResults.Height = 28;
        _searchResults.TextAlign = ContentAlignment.MiddleCenter;
        _searchResults.ForeColor = Theme.TextMuted;
        _searchResults.BackColor = Color.Transparent;

        ConfigureButton(_searchPreviousButton, IconKind.ChevronUp, null, () => _viewer.GoToPreviousMatch());
        ConfigureButton(_searchNextButton, IconKind.ChevronDown, null, () => _viewer.GoToNextMatch());
        ConfigureButton(_searchCloseButton, IconKind.Close, null, CloseSearch);
        _searchCloseButton.AccessibleName = Strings.Get("SearchClear");

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.Transparent,
        };
        layout.Controls.Add(_searchBoxHost);
        layout.Controls.Add(_matchCaseButton);
        layout.Controls.Add(_wholeWordButton);
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

        _prevPageButton.Icon = IconKind.PreviousPage;
        _prevPageButton.Dock = DockStyle.Left;
        _prevPageButton.Width = 28;
        _prevPageButton.CornerRadius = 4;
        _prevPageButton.AccessibleName = Strings.Get("PreviousPage");
        _prevPageButton.Click += (_, _) => GoToPage(_viewer.FirstVisiblePage - 1);

        _nextPageButton.Icon = IconKind.NextPage;
        _nextPageButton.Dock = DockStyle.Left;
        _nextPageButton.Width = 28;
        _nextPageButton.CornerRadius = 4;
        _nextPageButton.AccessibleName = Strings.Get("NextPage");
        _nextPageButton.Click += (_, _) => GoToPage(_viewer.FirstVisiblePage + 1);

        _pageLabel.AutoSize = false;
        _pageLabel.Dock = DockStyle.Left;
        _pageLabel.Width = 135;
        _pageLabel.TextAlign = ContentAlignment.MiddleCenter;
        _pageLabel.Padding = new Padding(4, 0, 4, 0);
        _pageLabel.ForeColor = Theme.TextMuted;
        _pageLabel.BackColor = Color.Transparent;
        _pageLabel.Click += (_, _) => BeginPageJump();

        _pageJumpField.Visible = false;
        _pageJumpField.BorderStyle = BorderStyle.FixedSingle;
        _pageJumpField.BackColor = Theme.Canvas;
        _pageJumpField.ForeColor = Theme.Text;
        _pageJumpField.TextAlign = HorizontalAlignment.Center;
        _pageJumpField.KeyDown += OnPageJumpKeyDown;
        _pageJumpField.LostFocus += (_, _) => EndPageJump(commit: false);

        _zoomLabel.AutoSize = false;
        _zoomLabel.Dock = DockStyle.Right;
        _zoomLabel.Width = 90;
        _zoomLabel.TextAlign = ContentAlignment.MiddleRight;
        _zoomLabel.Padding = new Padding(0, 0, 14, 0);
        _zoomLabel.ForeColor = Theme.TextMuted;
        _zoomLabel.BackColor = Color.Transparent;
        _zoomLabel.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && _document is not null)
            {
                ShowZoomMenu();
            }
        };
        _zoomLabel.DoubleClick += (_, _) =>
        {
            if (_document is not null)
            {
                _fitPageActive = false;
                ApplyZoom(1f);
            }
        };

        // Dock.Left places the last-added control at the far left edge.
        _statusBar.Controls.Add(_zoomLabel);
        _statusBar.Controls.Add(_nextPageButton);
        _statusBar.Controls.Add(_pageLabel);
        _statusBar.Controls.Add(_prevPageButton);
        _statusBar.Controls.Add(_pageJumpField);
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

        // 8. Fit page: the whole page must fit, so the resulting slot is never
        //    taller than the viewport.
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--fit-page", StringComparison.OrdinalIgnoreCase)))
        {
            var zoomBefore = _viewer.Zoom;
            FitPage();
            Application.DoEvents();
            var slot = _viewer.LayoutSlots.Count > 0 ? _viewer.LayoutSlots[0] : default;
            Console.Error.WriteLine(
                $"SHELL: fitPage zoom {zoomBefore:P0}->{_viewer.Zoom:P0} slot={slot.Width}x{slot.Height} " +
                $"viewport={_viewer.ClientSize.Width}x{_viewer.ClientSize.Height} " +
                $"fits={slot.Height <= _viewer.ClientSize.Height && slot.Width <= _viewer.ClientSize.Width}");
        }

        // 9. Panel persistence: the state is written and read back from the same
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
        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = Theme.Canvas;
        _contentHost.Padding = new Padding(0);

        _scrollBar.Dock = DockStyle.Right;
        _scrollBar.Width = 12;
        _scrollBar.Visible = false;

        _hScrollBar.Dock = DockStyle.Bottom;
        _hScrollBar.Height = 10;
        _hScrollBar.Visible = false;

        _viewer.Dock = DockStyle.Fill;
        _viewer.Visible = false;
        _viewer.Resize += (_, _) => PositionSearchPill();
        _viewer.Controls.Add(_searchBar);
        _welcomeView.Dock = DockStyle.Fill;
        _welcomeView.Visible = true;

        // WinForms lays docked children out from the last one added, so Fill
        // goes in first and the edges after it.
        _sidePanel.Dock = DockStyle.Left;
        _sidePanel.Width = 190;
        _sidePanel.Visible = false;

        _contentHost.Controls.Add(_viewer);
        _contentHost.Controls.Add(_welcomeView);
        _contentHost.Controls.Add(_hScrollBar);
        _contentHost.Controls.Add(_scrollBar);
        _contentHost.Controls.Add(_sidePanel);

        _tabStrip.Dock = DockStyle.Top;
        _tabStrip.Height = 32;

        Controls.Add(_contentHost);
        Controls.Add(_statusBar);
        Controls.Add(_toolbar);
        Controls.Add(_tabStrip);
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
        _toolTip.Attach(_fitPageButton, () => Strings.Get("FitPage"), () => "Alt+P");
        _toolTip.Attach(_viewModeButton, () => Strings.Get("ViewMode"), null);
        _toolTip.Attach(_darkPageButton, () => Strings.Get("DarkPageMode"), () => "Alt+N");
        _toolTip.Attach(_rotateLeftButton, () => Strings.Get("RotateLeft"), () => "Alt+R");
        _toolTip.Attach(_rotateRightButton, () => Strings.Get("RotateRight"), () => "Ctrl+R");
        _toolTip.Attach(_handToolButton, () => _viewer.ActiveTool == ViewerTool.Pan ? Strings.Get("SelectTool") : Strings.Get("HandTool"), () => "Alt+H");
        _toolTip.Attach(_copyButton, () => Strings.Get("Copy"), () => "Ctrl+C");
        _toolTip.Attach(_highlightButton, () => $"{Strings.Get("Highlight")} ({GetHighlightColorName(_settings.HighlightColor)})", () => "Alt+M");
        _toolTip.Attach(_bookmarkButton, () => IsCurrentPageBookmarked() ? Strings.Get("RemoveBookmark") : Strings.Get("AddBookmark"), () => "Ctrl+B");
        _toolTip.Attach(_prevPageButton, () => Strings.Get("PreviousPage"), null);
        _toolTip.Attach(_nextPageButton, () => Strings.Get("NextPage"), null);
        _toolTip.Attach(_pageLabel, () => Strings.Get("GoToPage"), () => "Ctrl+G");
        _toolTip.Attach(_zoomLabel, () => Strings.Get("ZoomLevel"), null);
        _toolTip.Attach(_searchButton, () => Strings.Get("Search"), () => "Ctrl+F");
        _toolTip.Attach(_printButton, () => Strings.Get("Print"), () => "Ctrl+P");
        _toolTip.Attach(_settingsButton, () => Strings.Get("Settings"), null);
        _toolTip.Attach(_sidePanelButton, () => Strings.Get("ToggleSidePanel"), () => "F4");
        _toolTip.Attach(_brandButton, () => Strings.Get("AboutTitle"), null);
        _toolTip.Attach(_themeButton, () => Strings.Get("ChangeTheme"), () => "Alt+T");
        _toolTip.Attach(_minimizeButton, () => Strings.Get("Minimize"),
            () => WindowState == FormWindowState.Maximized ? null : "Win+↓");
        _toolTip.Attach(_maximizeButton,
            () => WindowState == FormWindowState.Maximized ? Strings.Get("Restore") : Strings.Get("Maximize"),
            () => WindowState == FormWindowState.Maximized ? "Win+↓" : "Win+↑");
        _toolTip.Attach(_closeButton,
            () => CloseBehaviors.Normalize(_settings.CloseBehavior) == CloseBehaviors.Tray
                ? Strings.Get("CloseToTray")
                : Strings.Get("CloseWindow"),
            () => "Alt+F4");
        _toolTip.Attach(_searchPreviousButton, () => Strings.Get("SearchPrevious"), () => "Shift+F3");
        _toolTip.Attach(_searchNextButton, () => Strings.Get("SearchNext"), () => "F3");
        _toolTip.Attach(_searchCloseButton, () => Strings.Get("SearchClear"), () => "Esc");
        _toolTip.Attach(_matchCaseButton, () => Strings.Get("MatchCase"), null);
        _toolTip.Attach(_wholeWordButton, () => Strings.Get("WholeWord"), null);
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
        _hScrollBar.ValueChanged += (_, _) =>
        {
            if (_updatingScrollBar)
            {
                return;
            }
            _viewer.ScrollHorizontalTo(_hScrollBar.Value);
        };
        _viewer.ZoomRequested += (_, factor) => ChangeZoom(factor);
        _viewer.LinkHovered += (_, preview) =>
        {
            _linkHoverPreview = preview;
            UpdateStatus();
        };
        _viewer.LinkPageRequested += (_, _) => SaveViewState();

        _tabStrip.TabSelected += (_, index) =>
        {
            if (_showingStartTab && index == _sessions.Count)
            {
                return;
            }
            ActivateSession(index);
        };
        _tabStrip.TabCloseRequested += (_, index) =>
        {
            if (_showingStartTab && index == _sessions.Count)
            {
                CloseStartTab();
                return;
            }
            CloseTab(index);
        };
        _tabStrip.NewTabRequested += (_, _) => OpenStartTab();

        _welcomeView.OpenRequested += (_, _) => OpenFile();
        _welcomeView.FileRequested += (_, path) => OpenDocument(path);
        _welcomeView.RemoveRecentRequested += (_, path) =>
        {
            var normalized = NormalizePath(path);
            _settings.RecentFiles.RemoveAll(p => string.Equals(NormalizePath(p), normalized, StringComparison.OrdinalIgnoreCase));
            _store.Save(_settings);
            _welcomeView.SetRecentFiles(_settings.RecentFiles);
        };
        _welcomeView.ClearRecentRequested += (_, _) =>
        {
            _settings.RecentFiles.Clear();
            _store.Save(_settings);
            _welcomeView.SetRecentFiles(_settings.RecentFiles);
        };
        _welcomeView.DragEnter += ViewerDragEnter;
        _welcomeView.DragDrop += ViewerDragDrop;

        _viewer.ViewStateChanged += (_, _) =>
        {
            SyncScrollBar();
            UpdateStatus();
        };

        _viewer.SelectionChanged += (_, _) => UpdateSelectionState();
        _viewer.ViewStateChanged += (_, _) =>
        {
            UpdateBookmarkButtonState();
            if (_sidePanel.Visible)
            {
                _sidePanel.SetCurrentPage(_viewer.FirstVisiblePage);
                _sidePanel.EnsureAround(_viewer.FirstVisiblePage);
            }
        };
        _sidePanel.PageRequested += (_, page) => _viewer.ScrollToPage(page);
        _sidePanel.PersonalBookmarkDeleteRequested += (_, page) => RemovePersonalBookmark(page);
        _sidePanel.PersonalBookmarkRenameRequested += (_, args) => RenamePersonalBookmark(args.PageIndex, args.NewLabel);
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
            var showViewerBars = _document is not null && !_showingStartTab;
            _scrollBar.Visible = showViewerBars;
            _scrollBar.Maximum = Math.Max(0, _viewer.ContentHeight);
            _scrollBar.LargeChange = Math.Max(1, _viewer.ViewportHeight);
            _scrollBar.Value = _viewer.ScrollY;

            var needHorizontal = showViewerBars && _viewer.ContentWidth > _viewer.ClientSize.Width;
            _hScrollBar.Visible = needHorizontal;
            _hScrollBar.Maximum = Math.Max(0, _viewer.ContentWidth);
            _hScrollBar.LargeChange = Math.Max(1, _viewer.ClientSize.Width);
            _hScrollBar.Value = _viewer.ScrollX;
        }
        finally
        {
            _updatingScrollBar = false;
        }
        if (_searchBar.Visible)
        {
            PositionSearchPill();
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

    public void OpenDocument(string path, string? password = null, bool forceReload = false)
    {
        var normalized = NormalizePath(path);
        if (!forceReload)
        {
            var existing = _sessions.FindIndex(s => string.Equals(s.FilePath, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                ActivateSession(existing);
                return;
            }
        }

        UpdateStatus(Strings.Get("Loading"));
        _engine.OpenDocument(path, password, result =>
        {
            if (IsDisposed)
            {
                return;
            }
            BeginInvoke(() => OnDocumentOpened(path, result, forceReload));
        });
    }

    private void OnDocumentOpened(string path, PdfOpenResult result, bool replaceActive)
    {
        if (!result.Success)
        {
            if (result.Error is PdfOpenError.PasswordRequired or PdfOpenError.WrongPassword)
            {
                using var dialog = new PasswordDialog(
                    Path.GetFileName(path),
                    wrongPassword: result.Error == PdfOpenError.WrongPassword);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    OpenDocument(path, dialog.Password, replaceActive);
                    return;
                }
                UpdateStatus();
                return;
            }
            ShowOpenError(result.Error, result.PdfiumError);
            return;
        }

        if (result.Document is null)
        {
            return;
        }

        path = NormalizePath(path);
        PdfDocument? previousToClose = null;

        if (replaceActive && _activeSessionIndex >= 0 && _activeSessionIndex < _sessions.Count)
        {
            SaveCurrentSessionToMemory();
            previousToClose = _sessions[_activeSessionIndex].Document;
            _sessions[_activeSessionIndex] = new DocumentSession
            {
                Document = result.Document,
                FilePath = path,
                LayoutMode = _viewer.LayoutMode,
                DarkPageMode = _viewer.DarkPageMode,
            };
        }
        else
        {
            SaveCurrentSessionToMemory();
            _sessions.Add(new DocumentSession
            {
                Document = result.Document,
                FilePath = path,
                LayoutMode = _viewer.LayoutMode,
                DarkPageMode = _viewer.DarkPageMode,
            });
            _activeSessionIndex = _sessions.Count - 1;
        }

        _document = result.Document;
        _documentPath = path;
        _linkHoverPreview = null;
        _showingStartTab = false;

        _viewer.SetDocument(_document, dropCache: false);
        _welcomeView.Visible = false;
        _viewer.Visible = true;
        _sidePanel.SetDocument(null);
        _sidePanel.Width = _settings.SidePanelWidth;

        FitWidthOnOpen();

        if (previousToClose is not null)
        {
            _engine.CloseDocument(previousToClose);
        }

        Text = $"{Path.GetFileName(path)} — {Strings.Get("AppTitle")}";
        InvalidateTitleBar();
        RememberRecent(path);
        _welcomeView.SetRecentFiles(_settings.RecentFiles);
        ApplyStoredViewState(path);
        LoadDocumentAnnotations(path);

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

        SaveCurrentSessionToMemory();
        SyncTabStrip();
        SyncScrollBar();
        UpdateCommandStates();
        UpdateStatus();
        _viewer.Focus();
    }

    private void SaveCurrentSessionToMemory()
    {
        if (_activeSessionIndex < 0 || _activeSessionIndex >= _sessions.Count || _document is null)
        {
            return;
        }
        var s = _sessions[_activeSessionIndex];
        s.Zoom = _viewer.Zoom;
        s.Rotation = _viewer.Rotation;
        s.ScrollX = _viewer.ScrollX;
        s.ScrollY = _viewer.ScrollY;
        s.UserZoomed = _userZoomed;
        s.FitPageActive = _fitPageActive;
        s.SidePanelOpen = _sidePanel.Visible;
        s.SidePanelTab = _sidePanel.Mode;
        s.LayoutMode = _viewer.LayoutMode;
        s.DarkPageMode = _viewer.DarkPageMode;
    }

    private void ActivateSession(int index)
    {
        if (index < 0 || index >= _sessions.Count || (index == _activeSessionIndex && !_showingStartTab))
        {
            return;
        }

        if (!_showingStartTab)
        {
            SaveCurrentSessionToMemory();
            SaveViewState();
        }

        _showingStartTab = false;
        _activeSessionIndex = index;
        var target = _sessions[index];
        _document = target.Document;
        _documentPath = target.FilePath;
        _zoom = target.Zoom;
        _rotation = target.Rotation;
        _userZoomed = target.UserZoomed;
        _fitPageActive = target.FitPageActive;
        _linkHoverPreview = null;

        _viewer.RestoreSessionDocument(
            target.Document,
            target.Zoom,
            target.Rotation,
            target.ScrollX,
            target.ScrollY,
            target.LayoutMode,
            target.DarkPageMode);
        _welcomeView.Visible = false;
        _viewer.Visible = true;

        _sidePanel.SetDocument(null);
        if (target.SidePanelOpen)
        {
            _settings.SidePanelTab = target.SidePanelTab;
            SetSidePanelVisible(true, persist: false);
        }
        else
        {
            _sidePanel.Visible = false;
        }

        Text = $"{Path.GetFileName(target.FilePath)} — {Strings.Get("AppTitle")}";
        LoadDocumentAnnotations(target.FilePath);
        SyncTabStrip();
        SyncScrollBar();
        UpdateCommandStates();
        UpdateStatus();
        _viewer.Focus();
    }

    private void OpenStartTab()
    {
        if (_showingStartTab)
        {
            return;
        }
        if (_document is not null)
        {
            SaveCurrentSessionToMemory();
            SaveViewState();
        }
        _showingStartTab = true;
        _linkHoverPreview = null;
        if (_searchBar.Visible)
        {
            CloseSearch();
        }
        _viewer.Visible = false;
        _sidePanel.Visible = false;
        _scrollBar.Visible = false;
        _hScrollBar.Visible = false;
        _welcomeView.SetRecentFiles(_settings.RecentFiles);
        _welcomeView.Visible = true;
        Text = $"{Strings.Get("NewTabTitle")} — {Strings.Get("AppTitle")}";
        SyncTabStrip();
        UpdateCommandStates();
        UpdateStatus();
    }

    private void CloseStartTab()
    {
        if (!_showingStartTab)
        {
            return;
        }
        _showingStartTab = false;
        if (_sessions.Count > 0)
        {
            var target = Math.Clamp(_activeSessionIndex, 0, _sessions.Count - 1);
            _activeSessionIndex = -1;
            ActivateSession(target);
        }
        else
        {
            SyncTabStrip();
            UpdateCommandStates();
            UpdateStatus();
        }
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _sessions.Count)
        {
            return;
        }

        if (index == _activeSessionIndex && !_showingStartTab)
        {
            SaveCurrentSessionToMemory();
            SaveViewState();
        }

        var closing = _sessions[index];
        _sessions.RemoveAt(index);
        _engine.CloseDocument(closing.Document);

        if (_sessions.Count == 0)
        {
            _activeSessionIndex = -1;
            _showingStartTab = false;
            _document = null;
            _documentPath = null;
            _linkHoverPreview = null;
            _viewer.SetDocument(null);
            _viewer.Visible = false;
            ClearDocumentAnnotations();
            _welcomeView.Visible = true;
            _sidePanel.SetDocument(null);
            _sidePanel.Visible = false;
            Text = Strings.Get("AppTitle");
            SyncTabStrip();
            SyncScrollBar();
            UpdateCommandStates();
            UpdateStatus();
            return;
        }

        if (_showingStartTab)
        {
            if (_activeSessionIndex >= _sessions.Count)
            {
                _activeSessionIndex = _sessions.Count - 1;
            }
            SyncTabStrip();
            return;
        }

        var nextIndex = Math.Clamp(index, 0, _sessions.Count - 1);
        _activeSessionIndex = -1;
        ActivateSession(nextIndex);
    }

    private void SyncTabStrip()
    {
        var items = _sessions
            .Select(s => new DocumentTabItem(s.FilePath, Path.GetFileName(s.FilePath), s.FilePath))
            .ToList();
        var activeIdx = _activeSessionIndex;
        if (_showingStartTab)
        {
            items.Add(new DocumentTabItem("__start", Strings.Get("NewTabTitle"), Strings.Get("NewTabTitle")));
            activeIdx = items.Count - 1;
        }
        _tabStrip.SetTabs(items, activeIdx);
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
        var pageWidth = _rotation is 90 or 270 ? size.Height : size.Width;
        var twoPages = _viewer.LayoutMode == PageLayoutMode.TwoPage && _document.PageCount > 1;
        var scale = _viewer.DeviceDpi / 96f;
        if (scale <= 0)
        {
            scale = 1f;
        }
        // A proportional gutter reads better than a fixed one: at 150% zoom the
        // page should not run into the window edges.
        var viewportPixels = _viewer.ClientSize.Width;
        if (viewportPixels <= 0 || pageWidth <= 0)
        {
            return 1f;
        }
        var gutter = Math.Max(32, (int)(viewportPixels * 0.06));
        var spreadGap = twoPages ? (int)Math.Round(12 * scale) : 0;
        var available = viewportPixels - gutter * 2 - spreadGap;
        if (available <= 0)
        {
            return 1f;
        }
        return available / ((twoPages ? pageWidth * 2 : pageWidth) * scale);
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

    /// <summary>Shows the floating search pill and focuses the field.</summary>
    private void OpenSearch()
    {
        if (_document is null || _showingStartTab)
        {
            return;
        }

        PositionSearchPill();
        _searchBar.Visible = true;
        _searchBar.BringToFront();
        _searchButton.IsActive = true;
        _searchField.Focus();
        _searchField.SelectAll();
    }

    private void PositionSearchPill()
    {
        if (_viewer.ClientSize.Width <= 0)
        {
            return;
        }
        var scale = DeviceDpi / 96f;
        var margin = (int)Math.Round(12 * scale);
        var left = Math.Max(margin, _viewer.ClientSize.Width - _searchBar.Width - margin);
        _searchBar.Location = new Point(left, margin);
        _searchBar.BringToFront();
    }

    private void UpdateSearchPillRegion()
    {
        if (_searchBar.Width <= 2 || _searchBar.Height <= 2)
        {
            return;
        }
        var radius = (int)Math.Round(8 * (DeviceDpi / 96f));
        using var path = ThemePainter.RoundedRect(new Rectangle(0, 0, _searchBar.Width, _searchBar.Height), Math.Max(4, radius));
        var old = _searchBar.Region;
        _searchBar.Region = new Region(path);
        old?.Dispose();
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
        _sidePanelButton.IsActive = visible;
        if (visible)
        {
            _sidePanel.SetDocument(_document);
            _sidePanel.SetPersonalBookmarks(_currentAnnotations.Bookmarks);
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
        _searchButton.IsActive = false;
        _viewer.ClearSearch();
        _searchResults.Text = string.Empty;
        _viewer.Focus();
    }

    /// <summary>
    /// Runs the current query. The viewer reports progress as pages are
    /// extracted, so the counter updates while typing instead of freezing.
    /// </summary>
    private void RunSearch()
    {
        _viewer.Search(_searchField.Text, new SearchOptions
        {
            MatchCase = _matchCaseButton.IsActive,
            WholeWord = _wholeWordButton.IsActive,
        });
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
        var hasDocument = _document is not null && !_showingStartTab;
        var canActOnSelection = hasDocument && _viewer.HasSelection;
        _copyButton.Enabled = canActOnSelection;
        _highlightButton.Enabled = hasDocument;
        UpdateStatus();
    }

    private void LoadDocumentAnnotations(string path)
    {
        _currentAnnotations = AnnotationStore.GetForDocument(_annotations, path);
        _viewer.SetHighlights(_currentAnnotations.Highlights);
        _sidePanel.SetPersonalBookmarks(_currentAnnotations.Bookmarks);
        UpdateBookmarkButtonState();
    }

    private void ClearDocumentAnnotations()
    {
        _currentAnnotations = new DocumentAnnotations();
        _viewer.SetHighlights(null);
        _sidePanel.SetPersonalBookmarks(null);
        UpdateBookmarkButtonState();
    }

    private void PersistCurrentAnnotations()
    {
        if (_documentPath is null)
        {
            return;
        }
        AnnotationStore.SetForDocument(_annotations, _documentPath, _currentAnnotations);
        if (!_suppressPersistenceOnClose)
        {
            _annotationStore.Save(_annotations);
        }
    }

    private bool IsCurrentPageBookmarked()
    {
        if (_document is null || _showingStartTab)
        {
            return false;
        }
        var page = _viewer.FirstVisiblePage;
        return _currentAnnotations.Bookmarks.Any(b => b.PageIndex == page);
    }

    private void UpdateBookmarkButtonState()
    {
        var active = IsCurrentPageBookmarked();
        _bookmarkButton.IsActive = active;
        _bookmarkButton.Icon = active ? IconKind.BookmarkFilled : IconKind.Bookmark;
    }

    private void AddHighlight(string color)
    {
        if (_document is null || _showingStartTab || _viewer.Selection is not { IsEmpty: false } selection)
        {
            return;
        }

        var snippet = _viewer.GetSelectedText().Trim();
        if (snippet.Length > 120)
        {
            snippet = snippet.Substring(0, 120);
        }

        _currentAnnotations.Highlights.Add(new TextHighlight
        {
            Id = Guid.NewGuid().ToString("N"),
            PageIndex = selection.PageIndex,
            Start = selection.Start,
            Length = selection.Length,
            Color = HighlightColors.Normalize(color),
            Snippet = snippet,
        });

        PersistCurrentAnnotations();
        _viewer.SetHighlights(_currentAnnotations.Highlights);
        _viewer.ClearSelection();
    }

    private void RemoveHighlight(TextHighlight highlight)
    {
        if (_currentAnnotations.Highlights.RemoveAll(h => string.Equals(h.Id, highlight.Id, StringComparison.Ordinal) ||
                (h.PageIndex == highlight.PageIndex && h.Start == highlight.Start && h.Length == highlight.Length)) > 0)
        {
            PersistCurrentAnnotations();
            _viewer.SetHighlights(_currentAnnotations.Highlights);
        }
    }

    private void ToggleCurrentPageBookmark()
    {
        if (_document is null || _showingStartTab)
        {
            return;
        }

        var page = _viewer.FirstVisiblePage;
        if (_currentAnnotations.Bookmarks.RemoveAll(b => b.PageIndex == page) == 0)
        {
            _currentAnnotations.Bookmarks.Add(new PersonalBookmark
            {
                PageIndex = page,
                Label = Strings.Format("BookmarkPageLabel", page + 1),
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
            });
            _currentAnnotations.Bookmarks.Sort((a, b) => a.PageIndex.CompareTo(b.PageIndex));
        }

        PersistCurrentAnnotations();
        _sidePanel.SetPersonalBookmarks(_currentAnnotations.Bookmarks);
        UpdateBookmarkButtonState();
    }

    private void RemovePersonalBookmark(int pageIndex)
    {
        if (_currentAnnotations.Bookmarks.RemoveAll(b => b.PageIndex == pageIndex) > 0)
        {
            PersistCurrentAnnotations();
            _sidePanel.SetPersonalBookmarks(_currentAnnotations.Bookmarks);
            UpdateBookmarkButtonState();
        }
    }

    private void RenamePersonalBookmark(int pageIndex, string newLabel)
    {
        var updated = false;
        foreach (var bookmark in _currentAnnotations.Bookmarks)
        {
            if (bookmark.PageIndex == pageIndex)
            {
                bookmark.Label = newLabel;
                updated = true;
                break;
            }
        }
        if (updated)
        {
            PersistCurrentAnnotations();
            _sidePanel.SetPersonalBookmarks(_currentAnnotations.Bookmarks);
        }
    }

    private void OnHighlightButtonClick()
    {
        if (_document is null || _showingStartTab)
        {
            return;
        }
        if (_viewer.HasSelection)
        {
            AddHighlight(_settings.HighlightColor);
        }
        else
        {
            ShowHighlightColorMenu();
        }
    }

    private void ShowHighlightColorMenu()
    {
        if (_document is null || _showingStartTab)
        {
            return;
        }

        var current = HighlightColors.Normalize(_settings.HighlightColor);
        var items = new List<ContextMenuItem>();
        foreach (var color in HighlightColors.All)
        {
            var chosen = color;
            items.Add(new ContextMenuItem(
                GetHighlightColorName(chosen),
                () => SelectHighlightColor(chosen),
                chosen == current ? IconKind.Check : IconKind.None,
                true,
                Theme.HighlightColor(chosen)));
        }

        var menu = ThemedContextMenu.Create(items.ToArray());
        var anchor = _highlightButton.PointToScreen(new Point(0, _highlightButton.Height));
        menu.Show(anchor);
    }

    private void SelectHighlightColor(string color)
    {
        var normalized = HighlightColors.Normalize(color);
        _settings.HighlightColor = normalized;
        UpdateHighlightIndicator();
        if (!_suppressPersistenceOnClose)
        {
            _store.Save(_settings);
        }
        if (_viewer.HasSelection)
        {
            AddHighlight(normalized);
        }
    }

    private void UpdateHighlightIndicator()
        => _highlightButton.IndicatorColor = Theme.HighlightColor(_settings.HighlightColor);

    private static string GetHighlightColorName(string color)
        => HighlightColors.Normalize(color) switch
        {
            HighlightColors.Green => Strings.Get("HighlightGreen"),
            HighlightColors.Blue => Strings.Get("HighlightBlue"),
            HighlightColors.Pink => Strings.Get("HighlightPink"),
            _ => Strings.Get("HighlightYellow"),
        };

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
    /// plus highlight actions when text is selected or hovered over a highlight.
    /// </summary>
    private void ShowPageContextMenu(Point screenLocation, Point viewerLocation)
    {
        if (_document is null || _showingStartTab)
        {
            return;
        }

        var items = new List<ContextMenuItem>
        {
            new(Strings.Get("Copy"), CopySelection, IconKind.Copy, _viewer.HasSelection),
            new(Strings.Get("SelectAll"), () => _viewer.SelectAllOnCurrentPage(), IconKind.SelectAll, true),
        };

        if (_viewer.HasSelection)
        {
            items.Add(ContextMenuItem.Separator());
            items.Add(new ContextMenuItem($"{Strings.Get("Highlight")} ({Strings.Get("HighlightYellow")})", () => AddHighlight(HighlightColors.Yellow), IconKind.Highlight, true, Theme.HighlightColor(HighlightColors.Yellow)));
            items.Add(new ContextMenuItem($"{Strings.Get("Highlight")} ({Strings.Get("HighlightGreen")})", () => AddHighlight(HighlightColors.Green), IconKind.Highlight, true, Theme.HighlightColor(HighlightColors.Green)));
            items.Add(new ContextMenuItem($"{Strings.Get("Highlight")} ({Strings.Get("HighlightBlue")})", () => AddHighlight(HighlightColors.Blue), IconKind.Highlight, true, Theme.HighlightColor(HighlightColors.Blue)));
            items.Add(new ContextMenuItem($"{Strings.Get("Highlight")} ({Strings.Get("HighlightPink")})", () => AddHighlight(HighlightColors.Pink), IconKind.Highlight, true, Theme.HighlightColor(HighlightColors.Pink)));
        }
        else if (_viewer.HitTestHighlight(viewerLocation) is { } existing)
        {
            items.Add(ContextMenuItem.Separator());
            items.Add(new ContextMenuItem(Strings.Get("RemoveHighlight"), () => RemoveHighlight(existing), IconKind.Close, true));
        }

        items.Add(ContextMenuItem.Separator());
        var bookmarked = IsCurrentPageBookmarked();
        items.Add(new ContextMenuItem(
            bookmarked ? Strings.Get("RemoveBookmark") : Strings.Get("AddBookmark"),
            ToggleCurrentPageBookmark,
            bookmarked ? IconKind.BookmarkFilled : IconKind.Bookmark,
            true));
        items.Add(new ContextMenuItem(Strings.Get("Print"), PrintDocument, IconKind.Print, true));

        ThemedContextMenu.Create(items.ToArray()).Show(screenLocation);
    }

    private void ViewerMouseUpForContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && _document is not null)
        {
            ShowPageContextMenu(Cursor.Position, e.Location);
        }
    }

    private void ChangeZoom(float factor)
    {
        _userZoomed = true;
        _fitPageActive = false;
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
        UpdateZoomIndicators();
        UpdateStatus();
        SaveViewState();
    }

    private void FitWidth()
    {
        if (_document is null)
        {
            return;
        }
        _fitPageActive = false;
        _userZoomed = false;
        ApplyZoom(FitWidthZoomFor(_viewer.FirstVisiblePage), userInitiated: false);
    }

    /// <summary>
    /// Zoom that fits the whole page inside the viewport, both dimensions. Used
    /// for pages taller than the screen, where fit-width alone still leaves the
    /// reader scrolling to see a single page.
    /// </summary>
    private float FitPageZoomFor(int pageIndex)
    {
        if (_document is null)
        {
            return 1f;
        }
        var size = _document.GetPageSize(pageIndex);
        var pageWidth = _rotation is 90 or 270 ? size.Height : size.Width;
        var pageHeight = _rotation is 90 or 270 ? size.Width : size.Height;
        var scale = _viewer.DeviceDpi / 96f;
        if (scale <= 0)
        {
            scale = 1f;
        }

        var viewportWidth = _viewer.ClientSize.Width;
        var viewportHeight = _viewer.ClientSize.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0 || pageWidth <= 0 || pageHeight <= 0)
        {
            return 1f;
        }

        var gutter = Math.Max(24, (int)(Math.Min(viewportWidth, viewportHeight) * 0.04));
        var availableWidth = viewportWidth - gutter * 2;
        var availableHeight = viewportHeight - gutter * 2;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 1f;
        }

        // The tighter of the two constraints is what makes the whole page fit.
        var byWidth = availableWidth / (pageWidth * scale);
        var byHeight = availableHeight / (pageHeight * scale);
        return Math.Min(byWidth, byHeight);
    }

    private void FitPage()
    {
        if (_document is null)
        {
            return;
        }
        var page = _viewer.FirstVisiblePage;
        var zoom = Math.Clamp(FitPageZoomFor(page), 0.1f, 8f);

        // A rotation swaps the page's visual axes, so the computed zoom is for
        // the rotated page: GetPageSize already reports the rotated size, and
        // ApplyZoom rebuilds the layout, which is enough.
        _fitPageActive = true;
        ApplyZoom(zoom);
        _viewer.ScrollToPage(page);
    }

    private void UpdateZoomIndicators()
    {
        var hasDoc = _document is not null;
        _fitWidthButton.IsActive = hasDoc && !_userZoomed && !_fitPageActive;
        _fitPageButton.IsActive = hasDoc && _fitPageActive;
    }

    private void Rotate(int degrees)
    {
        if (_document is null)
        {
            return;
        }
        _rotation = ((_rotation + degrees) % 360 + 360) % 360;
        _viewer.SetRotation(_rotation);
        if (_fitPageActive)
        {
            FitPage();
        }
        else if (!_userZoomed)
        {
            FitWidthOnOpen();
        }
        SyncScrollBar();
        UpdateStatus();
        SaveViewState();
    }

    private void ToggleHandTool()
    {
        _viewer.ActiveTool = _viewer.ActiveTool == ViewerTool.Pan
            ? ViewerTool.Select
            : ViewerTool.Pan;
        _handToolButton.IsActive = _document is not null && _viewer.ActiveTool == ViewerTool.Pan;
    }

    private void GoToPage(int pageIndex)
    {
        if (_document is null)
        {
            return;
        }
        var clamped = Math.Clamp(pageIndex, 0, _document.PageCount - 1);
        _viewer.ScrollToPage(clamped);
        SyncScrollBar();
        UpdateStatus();
        SaveViewState();
    }

    private void BeginPageJump()
    {
        if (_document is null)
        {
            return;
        }

        var scale = DeviceDpi / 96f;
        var width = Math.Max(56, (int)Math.Round(68 * scale));
        var height = Math.Max(18, _statusBar.Height - 6);
        _pageJumpField.Bounds = new Rectangle(
            _pageLabel.Left + (int)Math.Round(6 * scale),
            (_statusBar.Height - height) / 2,
            width,
            height);
        _pageJumpField.ResetHistory((_viewer.FirstVisiblePage + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        _pageJumpField.Visible = true;
        _pageJumpField.BringToFront();
        _pageJumpField.Focus();
        _pageJumpField.SelectAll();
    }

    private void EndPageJump(bool commit)
    {
        if (!_pageJumpField.Visible)
        {
            return;
        }
        var text = _pageJumpField.Text.Trim();
        _pageJumpField.Visible = false;
        if (commit && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var pageNumber))
        {
            GoToPage(pageNumber - 1);
        }
        _viewer.Focus();
    }

    private void OnPageJumpKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            EndPageJump(commit: true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            EndPageJump(commit: false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ShowZoomMenu()
    {
        if (_document is null)
        {
            return;
        }

        var presets = new[] { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f, 3.0f };
        var items = new List<ContextMenuItem>();
        foreach (var preset in presets)
        {
            var zoomValue = preset;
            var label = Strings.Format("ZoomPercent", (int)Math.Round(zoomValue * 100));
            var isCurrent = Math.Abs(_viewer.Zoom - zoomValue) < 0.01f;
            items.Add(new ContextMenuItem(label, () =>
            {
                _fitPageActive = false;
                ApplyZoom(zoomValue);
            }, isCurrent ? IconKind.Check : IconKind.None));
        }

        items.Add(ContextMenuItem.Separator());
        items.Add(new ContextMenuItem(Strings.Get("FitWidth"), FitWidth, IconKind.FitWidth));
        items.Add(new ContextMenuItem(Strings.Get("FitPage"), FitPage, IconKind.FitPage));
        items.Add(new ContextMenuItem(Strings.Get("ZoomReset"), () =>
        {
            _fitPageActive = false;
            ApplyZoom(1f);
        }, IconKind.None));

        var menu = ThemedContextMenu.Create(items.ToArray());
        var anchor = _zoomLabel.PointToScreen(new Point(0, 0));
        menu.Show(anchor.X, anchor.Y - menu.PreferredSize.Height);
    }

    private void PrintDocument()
    {
        if (_document is null)
        {
            return;
        }
        PdfPrintService.Print(this, _engine, _document, _documentPath);
    }

    private void ReloadDocument()
    {
        if (_documentPath is null || !File.Exists(_documentPath))
        {
            return;
        }
        SaveViewState();
        OpenDocument(_documentPath, forceReload: true);
    }

    private void CloseCurrentDocument()
    {
        CloseTab(_activeSessionIndex);
    }

    private void ShowViewModeMenu()
    {
        if (_document is null)
        {
            return;
        }
        var mode = _viewer.LayoutMode;
        var menu = ThemedContextMenu.Create(
            new ContextMenuItem(Strings.Get("ViewContinuous"), () => SetViewMode(PageLayoutMode.Continuous), mode == PageLayoutMode.Continuous ? IconKind.Check : IconKind.ContinuousPage),
            new ContextMenuItem(Strings.Get("ViewSinglePage"), () => SetViewMode(PageLayoutMode.SinglePage), mode == PageLayoutMode.SinglePage ? IconKind.Check : IconKind.SinglePage),
            new ContextMenuItem(Strings.Get("ViewTwoPages"), () => SetViewMode(PageLayoutMode.TwoPage), mode == PageLayoutMode.TwoPage ? IconKind.Check : IconKind.TwoPages));
        var anchor = _viewModeButton.PointToScreen(new Point(0, _viewModeButton.Height));
        menu.Show(anchor);
    }

    private void SetViewMode(PageLayoutMode mode)
    {
        _viewer.SetLayoutMode(mode);
        if (!_userZoomed && !_fitPageActive)
        {
            RefitWidth();
        }
        SyncScrollBar();
        UpdateCommandStates();
        UpdateStatus();
    }

    private void ToggleDarkPageMode()
    {
        if (_document is null)
        {
            return;
        }
        _viewer.SetDarkPageMode(!_viewer.DarkPageMode);
        UpdateCommandStates();
    }

    private void ShowDocumentProperties()
    {
        if (_document is null)
        {
            return;
        }
        var doc = _document;
        var path = _documentPath;
        _engine.GetMetadata(
            doc,
            metadata =>
            {
                if (IsDisposed)
                {
                    return;
                }
                BeginInvoke(() =>
                {
                    if (IsDisposed || doc != _document)
                    {
                        return;
                    }
                    using var dialog = new DocumentPropertiesDialog(doc, metadata, path);
                    dialog.ShowDialog(this);
                });
            },
            error => Core.Diagnostics.AppLog.Warn($"Metadata read failed: {error.Message}"));
    }

    private void ShowSettingsDialog()
    {
        using var dialog = new SettingsDialog(
            _settings,
            theme =>
            {
                _settings.Theme = theme;
                Theme.Apply(theme);
                ApplyTheme();
                _store.Save(_settings);
            },
            lang => ApplyLanguage(lang),
            () => _store.Save(_settings),
            () => ShowAboutDialog());
        dialog.ShowDialog(this);
    }

    private void ShowAboutDialog(bool autoCheckUpdates = false)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        if (!Visible)
        {
            Show();
        }
        Activate();
        using var dialog = new AboutDialog(autoCheckUpdates);
        dialog.ShowDialog(this);
    }

    private void ShowSettingsMenu()
    {
        var hasDoc = _document is not null;
        var menu = ThemedContextMenu.Create(
            new ContextMenuItem(Strings.Get("ReloadDocument"), ReloadDocument, IconKind.Reload, hasDoc),
            new ContextMenuItem(Strings.Get("CloseDocument"), CloseCurrentDocument, IconKind.Close, hasDoc),
            new ContextMenuItem(Strings.Get("DocumentProperties"), ShowDocumentProperties, IconKind.Info, hasDoc),
            new ContextMenuItem(Strings.Get("Print"), PrintDocument, IconKind.Print, hasDoc),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Strings.Get("LanguageEn"), () => ApplyLanguage(Lang.En), Strings.Current == Lang.En ? IconKind.Check : IconKind.None),
            new ContextMenuItem(Strings.Get("LanguageEs"), () => ApplyLanguage(Lang.Es), Strings.Current == Lang.Es ? IconKind.Check : IconKind.None),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Strings.Get("Settings"), ShowSettingsDialog, IconKind.Settings),
            new ContextMenuItem(Strings.Get("AboutMenu"), () => ShowAboutDialog(), IconKind.Gem));

        var anchor = _settingsButton.PointToScreen(new Point(_settingsButton.Width - menu.PreferredSize.Width, _settingsButton.Height));
        menu.Show(anchor);
    }

    private void ApplyLanguage(Lang lang)
    {
        if (Strings.Current == lang)
        {
            return;
        }
        Strings.Current = lang;
        _settings.Language = lang == Lang.Es ? "es" : "en";
        _store.Save(_settings);
        RefreshLocalization();
    }

    private void RefreshLocalization()
    {
        _openButton.Text = Strings.Get("Open");
        _copyButton.Text = Strings.Get("Copy");
        _searchField.CueText = Strings.Get("SearchPlaceholder");
        _themeButton.AccessibleName = Strings.Get("ChangeTheme");
        _minimizeButton.AccessibleName = Strings.Get("Minimize");
        _maximizeButton.AccessibleName = WindowState == FormWindowState.Maximized
            ? Strings.Get("Restore")
            : Strings.Get("Maximize");
        _closeButton.AccessibleName = Strings.Get("CloseWindow");
        _prevPageButton.AccessibleName = Strings.Get("PreviousPage");
        _nextPageButton.AccessibleName = Strings.Get("NextPage");
        _searchCloseButton.AccessibleName = Strings.Get("SearchClear");
        _matchCaseButton.AccessibleName = Strings.Get("MatchCase");
        _wholeWordButton.AccessibleName = Strings.Get("WholeWord");
        _printButton.AccessibleName = Strings.Get("Print");
        _settingsButton.AccessibleName = Strings.Get("Settings");
        _viewModeButton.AccessibleName = Strings.Get("ViewMode");
        _darkPageButton.AccessibleName = Strings.Get("DarkPageMode");
        _highlightButton.AccessibleName = Strings.Get("Highlight");
        _bookmarkButton.AccessibleName = Strings.Get("ToggleBookmark");

        _welcomeView.RefreshLocalization();
        _sidePanel.Invalidate();
        UpdateSearchState();
        UpdateStatus();
        RescaleChrome();
        InvalidateTitleBar();
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
        _contentHost.BackColor = Theme.Canvas;
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
        _themeButton.AccessibleName = Strings.Get("ChangeTheme");
        UpdateHighlightIndicator();
        _sidePanel.BackColor = Theme.Surface;
        _sidePanel.Invalidate(true);
        _welcomeView.ApplyTheme();
        _searchBoxHost.BackColor = Theme.Surface;
        _searchBoxHost.Invalidate();
        _matchCaseButton.Invalidate();
        _wholeWordButton.Invalidate();
        _searchPreviousButton.Invalidate();
        _searchNextButton.Invalidate();
        _searchCloseButton.Invalidate();
        _prevPageButton.Invalidate();
        _nextPageButton.Invalidate();
        _pageJumpField.BackColor = Theme.Canvas;
        _pageJumpField.ForeColor = Theme.Text;
        _handToolButton.Invalidate();
        _viewModeButton.Invalidate();
        _darkPageButton.Invalidate();
        _printButton.Invalidate();
        _settingsButton.Invalidate();
        _tabStrip.BackColor = Theme.Bg;
        _tabStrip.Invalidate();
        _brandButton.Invalidate();

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
        _hScrollBar.Invalidate();
        _titleBar.Invalidate();
        _toolbar.Invalidate();
        _statusBar.Invalidate();
        _searchBar.Invalidate();
        Invalidate();
    }

    // --- Status -------------------------------------------------------------

    private void UpdateStatus(string? overrideText = null)
    {
        if (overrideText is not null)
        {
            _pageLabel.Text = overrideText;
            return;
        }

        if (_document is null || _showingStartTab)
        {
            _pageLabel.Text = _showingStartTab ? Strings.Get("NewTabTitle") : Strings.Get("NoDocument");
            _pageLabel.Cursor = Cursors.Default;
            _zoomLabel.Text = string.Empty;
            _zoomLabel.Cursor = Cursors.Default;
            _prevPageButton.Enabled = false;
            _nextPageButton.Enabled = false;
            InvalidateTitleBar();
            return;
        }

        var page = Math.Min(_viewer.FirstVisiblePage + 1, _document.PageCount);
        _pageLabel.Text = _linkHoverPreview ?? Strings.Format("PageOf", page, _document.PageCount);
        _pageLabel.Cursor = Cursors.Hand;
        _zoomLabel.Text = Strings.Format("ZoomPercent", (int)Math.Round(_viewer.Zoom * 100));
        _zoomLabel.Cursor = Cursors.Hand;
        _prevPageButton.Enabled = page > 1;
        _nextPageButton.Enabled = page < _document.PageCount;
        _pageLabel.Width = (int)Math.Round((_linkHoverPreview is not null ? 260 : 135) * (DeviceDpi / 96f));
        InvalidateTitleBar();
    }

    private void UpdateCommandStates()
    {
        var hasDocument = _document is not null && !_showingStartTab;
        _zoomInButton.Enabled = hasDocument;
        _zoomOutButton.Enabled = hasDocument;
        _fitWidthButton.Enabled = hasDocument;
        _fitPageButton.Enabled = hasDocument;
        _viewModeButton.Enabled = hasDocument;
        _viewModeButton.Icon = _viewer.LayoutMode switch
        {
            PageLayoutMode.SinglePage => IconKind.SinglePage,
            PageLayoutMode.TwoPage => IconKind.TwoPages,
            _ => IconKind.ContinuousPage,
        };
        _darkPageButton.Enabled = hasDocument;
        _darkPageButton.IsActive = hasDocument && _viewer.DarkPageMode;
        _rotateLeftButton.Enabled = hasDocument;
        _rotateRightButton.Enabled = hasDocument;
        _handToolButton.Enabled = hasDocument;
        _handToolButton.IsActive = hasDocument && _viewer.ActiveTool == ViewerTool.Pan;
        _copyButton.Enabled = hasDocument && _viewer.HasSelection;
        _highlightButton.Enabled = hasDocument;
        _bookmarkButton.Enabled = hasDocument;
        UpdateBookmarkButtonState();
        _searchButton.Enabled = hasDocument;
        _printButton.Enabled = hasDocument;
        _sidePanelButton.Enabled = hasDocument;
        if (!hasDocument)
        {
            _prevPageButton.Enabled = false;
            _nextPageButton.Enabled = false;
        }
        if (!hasDocument && _sidePanel.Visible)
        {
            _sidePanel.Visible = false;
        }
        _sidePanelButton.IsActive = hasDocument && _sidePanel.Visible;
        if (!hasDocument && _searchBar.Visible)
        {
            CloseSearch();
        }
        _searchButton.IsActive = hasDocument && _searchBar.Visible;
        UpdateZoomIndicators();
        UpdateTrayTooltip();
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
        if (_showingStartTab)
        {
            _titleLabel.ForeColor = Theme.Text;
            _titleLabel.Text = Strings.Get("NewTabTitle");
            PositionTitleLabel();
            return;
        }
        if (_documentPath is null)
        {
            _titleLabel.ForeColor = Theme.TextMuted;
            _titleLabel.Text = Strings.Get("AppSubtitle");
            PositionTitleLabel();
            return;
        }
        _titleLabel.ForeColor = Theme.Text;
        _titleLabel.Text = Path.GetFileName(_documentPath);
        PositionTitleLabel();
    }

    private void PositionTitleLabel()
    {
        if (_titleBar.Width <= 0)
        {
            return;
        }

        var scale = DeviceDpi / 96f;
        var margin = (int)Math.Round(10 * scale);
        var minLeft = _brandButton.Right + margin;
        var maxRight = _themeButton.Left - margin;
        var available = Math.Max(40, maxRight - minLeft);
        var symmetricHalf = Math.Max(20, Math.Min(_titleBar.Width / 2 - minLeft, maxRight - _titleBar.Width / 2));

        if (symmetricHalf * 2 >= Math.Min(available, (int)Math.Round(260 * scale)))
        {
            var width = symmetricHalf * 2;
            _titleLabel.Bounds = new Rectangle((_titleBar.Width - width) / 2, 0, width, _titleBar.Height);
        }
        else
        {
            _titleLabel.Bounds = new Rectangle(minLeft, 0, available, _titleBar.Height);
        }
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
        if (ActiveControl is TextBoxBase)
        {
            var isTextEditingKey =
                (e.Control && e.KeyCode is Keys.C or Keys.A or Keys.X or Keys.V or Keys.Z or Keys.Y)
                || e.KeyCode is Keys.Home or Keys.End or Keys.Up or Keys.Down or Keys.Left or Keys.Right;
            if (isTextEditingKey)
            {
                return;
            }
        }

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
            case Keys.P when e.Alt:
                FitPage();
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
            case Keys.G when e.Control:
                BeginPageJump();
                e.Handled = true;
                break;
            case Keys.P when e.Control:
                PrintDocument();
                e.Handled = true;
                break;
            case Keys.D when e.Control:
                ShowDocumentProperties();
                e.Handled = true;
                break;
            case Keys.F5:
                ReloadDocument();
                e.Handled = true;
                break;
            case Keys.H when e.Alt:
                ToggleHandTool();
                e.Handled = true;
                break;
            case Keys.N when e.Alt:
                ToggleDarkPageMode();
                e.Handled = true;
                break;
            case Keys.M when e.Alt:
                AddHighlight(_settings.HighlightColor);
                e.Handled = true;
                break;
            case Keys.B when e.Control:
                ToggleCurrentPageBookmark();
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
            case Keys.T when e.Control:
                OpenStartTab();
                e.Handled = true;
                break;
            case Keys.W when e.Control:
                if (_showingStartTab)
                {
                    CloseStartTab();
                }
                else if (_sessions.Count > 0)
                {
                    CloseTab(_activeSessionIndex);
                }
                else
                {
                    Close();
                }
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Tab))
        {
            var total = _sessions.Count + (_showingStartTab ? 1 : 0);
            if (total > 1)
            {
                var current = _showingStartTab ? _sessions.Count : _activeSessionIndex;
                var next = (current + 1) % total;
                if (next < _sessions.Count)
                {
                    ActivateSession(next);
                }
                else
                {
                    OpenStartTab();
                }
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool _userZoomed;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Padding = WindowState == FormWindowState.Maximized ? Padding.Empty : new Padding(1);
        if (_maximizeButton.Icon != IconKind.None)
        {
            _maximizeButton.Icon = WindowState == FormWindowState.Maximized ? IconKind.Restore : IconKind.Maximize;
        }
        // Keep the page fitted to the window while the user has not chosen a zoom
        // level explicitly, which is what every reader does on resize.
        if (!_userZoomed && !_fitPageActive && _document is not null && WindowState != FormWindowState.Minimized)
        {
            FitWidthOnOpen();
            SyncScrollBar();
            UpdateStatus();
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (WindowState != FormWindowState.Maximized && ClientSize.Width > 1 && ClientSize.Height > 1)
        {
            using var border = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        RescaleChrome();

        if (!_suppressPersistenceOnClose && _trayController is null)
        {
            InitializeTrayController();
        }

        if (_resizeFilter is null)
        {
            _resizeFilter = new ResizeBorderFilter(this);
            Application.AddMessageFilter(_resizeFilter);
        }

        if (TryGetValidStoredBounds(out var storedBounds))
        {
            Bounds = storedBounds;
            if (_settings.WindowMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }
            return;
        }

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

    private bool TryGetValidStoredBounds(out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (!_settings.RestoreSession)
        {
            return false;
        }
        if (_settings.WindowX is not int x
            || _settings.WindowY is not int y
            || _settings.WindowWidth is not int width
            || _settings.WindowHeight is not int height)
        {
            return false;
        }
        if (width < 560 || height < 420 || width > 7680 || height > 4320)
        {
            return false;
        }
        var candidate = new Rectangle(x, y, width, height);
        if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(candidate)))
        {
            return false;
        }
        bounds = candidate;
        return true;
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
        _hScrollBar.Height = Math.Max(8, (int)Math.Round(10 * scale));
        _titleBar.Height = (int)Math.Round(TitleBarHeight * scale);
        _brandButton.Width = (int)Math.Round(114 * scale);
        PositionTitleLabel();
        _tabStrip.Height = (int)Math.Round(32 * scale);
        _toolbar.Height = (int)Math.Round(ToolbarHeight * scale);
        _settingsButton.Width = (int)Math.Round(42 * scale);
        _statusBar.Height = (int)Math.Round(StatusBarHeight * scale);
        _prevPageButton.Width = (int)Math.Round(28 * scale);
        _nextPageButton.Width = (int)Math.Round(28 * scale);
        _searchBar.Size = new Size((int)Math.Round(444 * scale), (int)Math.Round(42 * scale));
        _searchBoxHost.Size = new Size((int)Math.Round(188 * scale), (int)Math.Round(28 * scale));
        _searchBoxHost.Padding = new Padding(
            (int)Math.Round(8 * scale),
            (int)Math.Round(5 * scale),
            (int)Math.Round(8 * scale),
            (int)Math.Round(4 * scale));
        _searchResults.Width = (int)Math.Round(72 * scale);
        _searchResults.Height = (int)Math.Round(28 * scale);
        _matchCaseButton.Size = _matchCaseButton.MeasurePreferredSize(scale);
        _wholeWordButton.Size = _wholeWordButton.MeasurePreferredSize(scale);
        _searchPreviousButton.Size = new Size((int)Math.Round(28 * scale), (int)Math.Round(28 * scale));
        _searchNextButton.Size = new Size((int)Math.Round(28 * scale), (int)Math.Round(28 * scale));
        _searchCloseButton.Size = new Size((int)Math.Round(28 * scale), (int)Math.Round(28 * scale));
        if (_searchBar.Controls.Count > 0 && _searchBar.Controls[0] is FlowLayoutPanel searchFlow)
        {
            searchFlow.Padding = new Padding(
                (int)Math.Round(8 * scale),
                (int)Math.Round(6 * scale),
                (int)Math.Round(8 * scale),
                (int)Math.Round(6 * scale));
        }
        PositionSearchPill();
        _pageLabel.Width = (int)Math.Round((_document is null || _showingStartTab ? 210 : 135) * scale);

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

    internal void SuppressPersistenceOnClose() => _suppressPersistenceOnClose = true;

    private void InitializeTrayController()
    {
        _trayController = new TrayController(
            Handle,
            Icon,
            () => Visible && WindowState != FormWindowState.Minimized,
            () => _settings.RecentFiles,
            ToggleTrayWindowVisibility,
            () =>
            {
                ShowFromTray();
                OpenFile();
            },
            path =>
            {
                ShowFromTray();
                OpenDocument(path);
            },
            () =>
            {
                ShowFromTray();
                ShowSettingsDialog();
            },
            autoCheckUpdates => ShowAboutDialog(autoCheckUpdates),
            ForceExitApplication);
        UpdateTrayTooltip();
    }

    private void ForceExitApplication()
    {
        _forceExit = true;
        Close();
    }

    private void PromptCloseBehavior()
    {
        if (_closePromptOpen || IsDisposed)
        {
            return;
        }
        _closePromptOpen = true;
        try
        {
            using var dialog = new CloseConfirmDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (dialog.RememberChoice)
            {
                _settings.CloseBehavior = CloseBehaviors.Normalize(dialog.Choice);
                _store.Save(_settings);
            }

            if (dialog.Choice == CloseBehaviors.Tray)
            {
                Hide();
            }
            else
            {
                ForceExitApplication();
            }
        }
        finally
        {
            _closePromptOpen = false;
        }
    }

    private void ToggleTrayWindowVisibility()
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            Hide();
        }
        else
        {
            ShowFromTray();
        }
    }

    private void ShowFromTray()
    {
        if (!Visible)
        {
            Show();
        }
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Activate();
    }

    private void UpdateTrayTooltip()
    {
        var segment = _showingStartTab || _documentPath is null
            ? null
            : Path.GetFileName(_documentPath);
        _trayController?.UpdateTooltip(segment);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_forceExit && !_suppressPersistenceOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            var behavior = CloseBehaviors.Normalize(_settings.CloseBehavior);
            if (behavior == CloseBehaviors.Tray)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            if (behavior == CloseBehaviors.Ask)
            {
                e.Cancel = true;
                BeginInvoke(PromptCloseBehavior);
                return;
            }
        }

        if (!_suppressPersistenceOnClose)
        {
            SaveViewState();
            PersistWindowBounds();
        }
        foreach (var session in _sessions)
        {
            _engine.CloseDocument(session.Document);
        }
        _sessions.Clear();
        _document = null;
        _toolTip?.Dispose();
        _trayController?.Dispose();
        _trayController = null;
        if (_resizeFilter is not null)
        {
            Application.RemoveMessageFilter(_resizeFilter);
            _resizeFilter = null;
        }
        _appIconBitmap?.Dispose();
        base.OnFormClosing(e);
    }

    /// <summary>Writes the window size and location for the next session.</summary>
    public void PersistWindowBounds()
    {
        _settings.WindowMaximized = WindowState == FormWindowState.Maximized;
        var rect = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (rect.Width >= 560 && rect.Height >= 420)
        {
            _settings.WindowX = rect.X;
            _settings.WindowY = rect.Y;
            _settings.WindowWidth = rect.Width;
            _settings.WindowHeight = rect.Height;
        }
        _store.Save(_settings);
    }

    private static void LoadEmbeddedIcons(out Icon? windowIcon, out Image? titleBitmap)
    {
        windowIcon = null;
        titleBitmap = null;
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames();

        var icoName = names.FirstOrDefault(n => n.EndsWith("CyberPDF.ico", StringComparison.OrdinalIgnoreCase));
        if (icoName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(icoName);
            if (stream is not null)
            {
                windowIcon = new Icon(stream);
            }
        }

        var pngName = names.FirstOrDefault(n => n.EndsWith("CyberPDF.png", StringComparison.OrdinalIgnoreCase));
        if (pngName is not null)
        {
            using var stream = assembly.GetManifestResourceStream(pngName);
            if (stream is not null)
            {
                titleBitmap = Image.FromStream(stream);
            }
        }
    }

    /// <summary>
    /// Enables 8-edge native Windows resize on a borderless form without
    /// breaking DrawToBitmap or child hit-testing.
    /// </summary>
    private sealed class ResizeBorderFilter : IMessageFilter
    {
        private const int WmMouseMove = 0x0200;
        private const int WmLButtonDown = 0x0201;

        private readonly MainWindow _form;

        public ResizeBorderFilter(MainWindow form) => _form = form;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is not (WmMouseMove or WmLButtonDown))
            {
                return false;
            }
            if (_form.IsDisposed || !_form.Visible || _form.WindowState != FormWindowState.Normal)
            {
                return false;
            }
            if (Control.FromHandle(m.HWnd)?.FindForm() != _form)
            {
                return false;
            }

            var pt = _form.PointToClient(Cursor.Position);
            var hit = HitTestBorder(pt);
            if (hit == 0)
            {
                return false;
            }

            if (m.Msg == WmMouseMove)
            {
                Cursor.Current = hit switch
                {
                    10 or 11 => Cursors.SizeWE,
                    12 or 15 => Cursors.SizeNS,
                    13 or 17 => Cursors.SizeNWSE,
                    14 or 16 => Cursors.SizeNESW,
                    _ => Cursors.Default,
                };
                return false;
            }

            ReleaseCapture();
            SendMessage(_form.Handle, WmNcLeftButtonDown, (IntPtr)hit, IntPtr.Zero);
            return true;
        }

        private int HitTestBorder(Point pt)
        {
            var w = _form.ClientSize.Width;
            var h = _form.ClientSize.Height;
            var grip = Math.Max(4, (int)Math.Round(5 * _form.DeviceDpi / 96f));

            var left = pt.X >= 0 && pt.X < grip;
            var right = pt.X <= w && pt.X >= w - grip;
            var top = pt.Y >= 0 && pt.Y < grip;
            var bottom = pt.Y <= h && pt.Y >= h - grip;

            if (top && left) return 13;     // HTTOPLEFT
            if (top && right) return 14;    // HTTOPRIGHT
            if (bottom && left) return 16;  // HTBOTTOMLEFT
            if (bottom && right) return 17; // HTBOTTOMRIGHT
            if (left) return 10;            // HTLEFT
            if (right) return 11;           // HTRIGHT
            if (top) return 12;             // HTTOP
            if (bottom) return 15;          // HTBOTTOM
            return 0;
        }
    }

    private sealed class DocumentSession
    {
        public required PdfDocument Document { get; init; }

        public required string FilePath { get; set; }

        public float Zoom { get; set; } = 1f;

        public int Rotation { get; set; }

        public int ScrollX { get; set; }

        public int ScrollY { get; set; }

        public bool UserZoomed { get; set; }

        public bool FitPageActive { get; set; }

        public bool SidePanelOpen { get; set; }

        public int SidePanelTab { get; set; }

        public PageLayoutMode LayoutMode { get; set; } = PageLayoutMode.Continuous;

        public bool DarkPageMode { get; set; }
    }
}
