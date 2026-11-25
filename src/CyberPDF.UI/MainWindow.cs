using System.Diagnostics;
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
        _searchField.BackColor = Theme.Canvas;
        _searchField.ForeColor = Theme.Text;
        _searchResults.ForeColor = Theme.TextMuted;
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.AutoEllipsis = true;
        _titleLabel.MouseDown += TitleBarMouseDown;
        _titleLabel.MouseDoubleClick += (_, _) => ToggleMaximize();

        _titleBar.MouseDown += TitleBarMouseDown;
        _titleBar.MouseDoubleClick += (_, _) => ToggleMaximize();
        _titleBar.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Clicks == 2)
            {
                ToggleMaximize();
            }
        };

        var close = new ToolButton
        {
            Icon = IconKind.Close,
            Dock = DockStyle.Right,
            Width = 46,
            Height = TitleBarHeight,
            AccessibleName = Strings.Get("CloseDocument"),
        };
        close.Click += (_, _) => Close();

        var theme = _themeButton;
        theme.Icon = IconKind.ThemeDefault;
        theme.Dock = DockStyle.Right;
        theme.Width = 40;
        theme.Height = TitleBarHeight;
        theme.Click += (_, _) => CycleTheme();

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(close);
        _titleBar.Controls.Add(theme);
        Controls.Add(_titleBar);
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

        flow.Controls.Add(_openButton);
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
        Controls.Add(_toolbar);
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
        Controls.Add(_statusBar);
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

        // Dock order matters: a Right-docked bar must be added BEFORE the
        // Fill control, because Fill claims whatever is left when it is added.
        // Adding Fill first makes it span the full width and cover the bar.
        host.Controls.Add(_scrollBar);
        host.Controls.Add(_viewer);
        Controls.Add(host);
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
        };
        _store.Save(_settings);
    }

    // --- Window chrome ------------------------------------------------------

    private bool _dragWindow;
    private Point _dragCursor;
    private Point _dragWindowStart;

    private void TitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        _dragWindow = true;
        _dragCursor = Cursor.Position;
        _dragWindowStart = Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragWindow)
        {
            return;
        }
        var delta = new Point(Cursor.Position.X - _dragCursor.X, Cursor.Position.Y - _dragCursor.Y);
        Location = new Point(_dragWindowStart.X + delta.X, _dragWindowStart.Y + delta.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragWindow = false;
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

    /// <summary>Attaches the single-instance helper so WndProc can route to it.</summary>
    public void AttachSingleInstance(SingleInstance singleInstance)
    {
        _singleInstance = singleInstance;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveViewState();
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
