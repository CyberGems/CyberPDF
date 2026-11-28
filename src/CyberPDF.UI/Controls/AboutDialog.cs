using CyberPDF.UI.Services;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CyberPDF.Common.I18n;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Frameless "About CyberPDF" modal adapted from CyberNotes's About layout:
/// hero icon and version, GitHub update check divider, four randomized sibling
/// suite apps with local icons, and footer links plus diagnostics copy.
/// </summary>
public sealed class AboutDialog : Form
{
    private async Task GiveStarAsync()
    {
        _starBtn.Enabled = false;
        _starStatusKey = "GitHubStarBusy";
        _starBtn.AccessibleName = Strings.Get(_starStatusKey);
        var result = await GitHubStarService.StarAsync();
        if (IsDisposed) return;
        _starStatusKey = $"GitHubStar{result}";
        _starBtn.AccessibleName = Strings.Get(_starStatusKey);
        _starBtn.Icon = result == GitHubStarResult.Starred ? IconKind.StarFilled : IconKind.Star;
        _starBtn.Enabled = result != GitHubStarResult.Starred;
    }

    private const string RepoUrl = "https://github.com/CyberGems/CyberPDF";
    private const string WikiUrl = $"{RepoUrl}/wiki";
    private const string WikiEsUrl = $"{RepoUrl}/wiki/Home-es";
    private const string DonateUrl = $"{RepoUrl}#%EF%B8%8F-donate";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/CyberGems/CyberPDF/releases/latest";
    private const string SiteUrl = "https://cybergems.org";
    private const string SuiteUrl = $"{SiteUrl}/#apps";

    private sealed record SuiteAppEntry(string Slug, string FileName, string PitchKey);

    private static readonly SuiteAppEntry[] SuitePool =
    [
        new("cyberclock", "cyberclock.png", "SuiteClock"),
        new("cyberfeeds", "cyberfeeds.png", "SuiteFeeds"),
        new("cyberlauncher", "cyberlauncher.png", "SuiteLauncher"),
        new("cybermanager", "cybermanager.png", "SuiteManager"),
        new("cybernotes", "cybernotes.png", "SuiteNotes"),
        new("cyberpaste", "cyberpaste.png", "SuitePaste"),
        new("cybersnap", "cybersnap.png", "SuiteSnap"),
        new("cybertray", "cybertray.png", "SuiteTray"),
        new("cyberviewer", "cyberviewer.png", "SuiteViewer"),
        new("cyberwall", "cyberwall.png", "SuiteWall"),
    ];

    private static readonly HttpClient SharedHttp = CreateHttpClient();

    private readonly Image? _appBitmap;
    private readonly List<Image> _ownedImages = [];
    private readonly ThemedToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _copiedTimer = new() { Interval = 1800 };
    private CancellationTokenSource? _checkCts;

    private readonly Panel _headerBar = new();
    private readonly ToolButton _closeButton = new();
    private readonly ToolButton _checkUpdatesBtn = new();
    private readonly Label _statusLabel = new();
    private readonly ToolButton _copyDiagBtn = new();
    private readonly ToolButton _donateBtn = new();
    private readonly ToolButton _starBtn = new();
    private string _starStatusKey = "GitHubStar";
    private readonly Panel _footerBar = new();

    private string? _availableReleaseUrl;
    private bool _checking;
    private bool _diagCopied;

    public AboutDialog(bool autoCheckUpdates = false)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Padding = new Padding(1);
        DoubleBuffered = true;
        KeyPreview = true;
        Text = Strings.Get("AboutTitle");

        _appBitmap = LoadEmbeddedImage("CyberPDF.png");
        if (_appBitmap is not null)
        {
            _ownedImages.Add(_appBitmap);
        }

        _copiedTimer.Tick += (_, _) =>
        {
            _copiedTimer.Stop();
            _diagCopied = false;
            _copyDiagBtn.Icon = IconKind.Copy;
            _copyDiagBtn.Invalidate();
        };

        var scale = DeviceDpi <= 0 ? 1f : DeviceDpi / 96f;
        ClientSize = new Size((int)Math.Round(440 * scale), (int)Math.Round(556 * scale));

        BuildContent(scale);

        if (autoCheckUpdates)
        {
            Shown += (_, _) => _ = CheckForUpdatesAsync();
        }
    }

    private void BuildContent(float scale)
    {
        int S(int px) => (int)Math.Round(px * scale);

        _headerBar.Bounds = new Rectangle(1, 1, ClientSize.Width - 2, S(36));
        _headerBar.BackColor = Color.Transparent;
        _headerBar.MouseDown += DragMouseDown;

        _closeButton.Icon = IconKind.Close;
        _closeButton.Bounds = new Rectangle(_headerBar.Width - S(36), S(4), S(32), S(28));
        _closeButton.IsWindowChrome = true;
        _closeButton.IsDanger = true;
        _closeButton.CornerRadius = 6;
        _closeButton.AccessibleName = Strings.Get("CloseWindow");
        _closeButton.Click += (_, _) => Close();
        _toolTip.Attach(_closeButton, () => Strings.Get("CloseWindow"), () => "Esc");
        _headerBar.Controls.Add(_closeButton);
        Controls.Add(_headerBar);

        var pad = S(28);
        var innerW = ClientSize.Width - pad * 2;
        var y = S(26);

        var heroIcon = new Panel
        {
            Bounds = new Rectangle((ClientSize.Width - S(68)) / 2, y, S(68), S(68)),
            BackColor = Color.Transparent,
        };
        heroIcon.Paint += (_, e) =>
        {
            if (_appBitmap is null)
            {
                return;
            }
            ThemePainter.PrepareHighQuality(e.Graphics);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(_appBitmap, 0, 0, heroIcon.Width, heroIcon.Height);
        };
        heroIcon.MouseDown += DragMouseDown;
        Controls.Add(heroIcon);
        y += S(78);

        // Two-tone heading matching CyberNotes ("Cyber" + accent "PDF").
        var titlePanel = new Panel
        {
            Bounds = new Rectangle(pad, y, innerW, S(34)),
            BackColor = Color.Transparent,
        };
        titlePanel.Paint += (_, e) =>
        {
            ThemePainter.PrepareHighQuality(e.Graphics);
            using var titleFont = new Font("Segoe UI", 17.5f, FontStyle.Bold, GraphicsUnit.Point);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var leftSize = TextRenderer.MeasureText(e.Graphics, "Cyber", titleFont, new Size(int.MaxValue, int.MaxValue), flags);
            var rightSize = TextRenderer.MeasureText(e.Graphics, "PDF", titleFont, new Size(int.MaxValue, int.MaxValue), flags);
            var totalW = leftSize.Width + rightSize.Width;
            var startX = (titlePanel.Width - totalW) / 2;
            var textY = (titlePanel.Height - leftSize.Height) / 2;
            TextRenderer.DrawText(e.Graphics, "Cyber", titleFont, new Point(startX, textY), Theme.Text, flags);
            TextRenderer.DrawText(e.Graphics, "PDF", titleFont, new Point(startX + leftSize.Width, textY), Theme.Accent, flags);
        };
        Controls.Add(titlePanel);
        y += S(34);

        var verLabel = new Label
        {
            Bounds = new Rectangle(pad, y, innerW, S(20)),
            BackColor = Color.Transparent,
            ForeColor = Theme.TextMuted,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = Strings.Format("VersionLabel", FormatAppVersion()).ToUpperInvariant(),
        };
        Controls.Add(verLabel);
        y += S(26);

        var descLabel = new Label
        {
            Bounds = new Rectangle(pad, y, innerW, S(40)),
            BackColor = Color.Transparent,
            ForeColor = Theme.TextMuted,
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.TopCenter,
            Text = Strings.Get("AboutDesc"),
        };
        Controls.Add(descLabel);
        y += S(48);

        Controls.Add(BuildSectionHeader(pad, y, innerW, S(18), Strings.Get("AboutMaintenance")));
        y += S(24);

        _statusLabel.Bounds = new Rectangle(pad, y, innerW, S(20));
        _statusLabel.BackColor = Color.Transparent;
        _statusLabel.ForeColor = Theme.TextMuted;
        _statusLabel.Font = new Font("Segoe UI", 8.75f, FontStyle.Regular, GraphicsUnit.Point);
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Visible = false;
        _statusLabel.Cursor = Cursors.Default;
        _statusLabel.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_availableReleaseUrl))
            {
                OpenUrl(_availableReleaseUrl);
            }
        };
        Controls.Add(_statusLabel);
        y += S(22);

        _checkUpdatesBtn.Icon = IconKind.Reload;
        _checkUpdatesBtn.Text = Strings.Get("CheckUpdates");
        _checkUpdatesBtn.ShowBorder = true;
        _checkUpdatesBtn.CornerRadius = 6;
        var checkWidth = Math.Max(S(184), _checkUpdatesBtn.MeasurePreferredSize(scale).Width + S(12));
        _checkUpdatesBtn.Bounds = new Rectangle((ClientSize.Width - checkWidth) / 2, y, checkWidth, S(32));
        _checkUpdatesBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_availableReleaseUrl))
            {
                OpenUrl(_availableReleaseUrl);
                return;
            }
            _ = CheckForUpdatesAsync();
        };
        Controls.Add(_checkUpdatesBtn);
        y += S(50);

        Controls.Add(BuildSectionHeader(pad, y, innerW, S(18), Strings.Get("AboutSuiteTitle")));
        y += S(26);

        var picks = PickRandomSuiteApps(4);
        var btnSize = S(42);
        var gap = S(10);
        var rowW = picks.Count * btnSize + Math.Max(0, picks.Count - 1) * gap;
        var rx = (ClientSize.Width - rowW) / 2;
        foreach (var app in picks)
        {
            var iconImg = LoadEmbeddedImage(app.FileName);
            if (iconImg is not null)
            {
                _ownedImages.Add(iconImg);
            }

            var btn = new ToolButton
            {
                Bounds = new Rectangle(rx, y, btnSize, btnSize),
                ShowBorder = true,
                CornerRadius = 10,
                AccessibleName = Strings.Get(app.PitchKey),
            };
            btn.Paint += (_, e) =>
            {
                if (iconImg is null)
                {
                    return;
                }
                ThemePainter.PrepareHighQuality(e.Graphics);
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                var imgSize = S(26);
                var ix = (btn.Width - imgSize) / 2;
                var iy = (btn.Height - imgSize) / 2;
                e.Graphics.DrawImage(iconImg, ix, iy, imgSize, imgSize);
            };
            var url = $"{SiteUrl}/apps/{app.Slug}/";
            var pitchKey = app.PitchKey;
            btn.Click += (_, _) => OpenUrl(url);
            _toolTip.Attach(btn, () => Strings.Get(pitchKey), null);
            Controls.Add(btn);
            rx += btnSize + gap;
        }
        y += btnSize + S(8);

        var moreBtn = new ToolButton
        {
            Text = $"{Strings.Get("AboutSuiteMore")} →",
            CornerRadius = 6,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point),
        };
        var moreSize = moreBtn.MeasurePreferredSize(scale);
        moreBtn.Bounds = new Rectangle((ClientSize.Width - moreSize.Width) / 2, y, moreSize.Width, S(26));
        moreBtn.Click += (_, _) => OpenUrl(SuiteUrl);
        Controls.Add(moreBtn);

        _footerBar.Dock = DockStyle.Bottom;
        _footerBar.Height = S(48);
        _footerBar.BackColor = Theme.Surface;
        _footerBar.Paint += (_, e) =>
        {
            using var border = new Pen(Theme.Border);
            e.Graphics.DrawLine(border, 0, 0, _footerBar.Width, 0);
        };
        Controls.Add(_footerBar);

        var brandBtn = new ToolButton
        {
            Text = "© CyberGems · 2026",
            CornerRadius = 6,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
        };
        var brandSize = brandBtn.MeasurePreferredSize(scale);
        brandBtn.Bounds = new Rectangle(S(14), (_footerBar.Height - S(28)) / 2, brandSize.Width, S(28));
        brandBtn.Click += (_, _) => OpenUrl(SiteUrl);
        _toolTip.Attach(brandBtn, () => Strings.Get("WebsiteTooltip"), null);
        _footerBar.Controls.Add(brandBtn);

        var linkButtons = new (ToolButton Button, IconKind Icon, string TooltipKey, Action OnClick)[]
        {
            (new ToolButton(), IconKind.Gem, "WebsiteTooltip", () => OpenUrl(SiteUrl)),
            (new ToolButton(), IconKind.BookOpen, "DocsTooltip", () => OpenUrl(Strings.Current == Lang.Es ? WikiEsUrl : WikiUrl)),
            (new ToolButton(), IconKind.GitHub, "GitHubTooltip", () => OpenUrl(RepoUrl)),
            (new ToolButton(), IconKind.Bug, "IssuesTooltip", () => OpenUrl($"{RepoUrl}/issues")),
            (_copyDiagBtn, IconKind.Copy, "CopyDiagnostics", CopyDiagnosticsToClipboard),
            (new ToolButton(), IconKind.Tag, "ReleasesTooltip", () => OpenUrl($"{RepoUrl}/releases")),
            (_starBtn, IconKind.Star, "GitHubStar", () => _ = GiveStarAsync()),
            (_donateBtn, IconKind.Heart, "DonateTooltip", () => OpenUrl(DonateUrl)),
        };

        var iconBtnSize = S(28);
        var iconGap = S(4);
        var rightX = ClientSize.Width - S(14) - iconBtnSize;
        for (var i = linkButtons.Length - 1; i >= 0; i--)
        {
            var entry = linkButtons[i];
            entry.Button.Icon = entry.Icon;
            entry.Button.CornerRadius = 6;
            entry.Button.Bounds = new Rectangle(rightX, (_footerBar.Height - iconBtnSize) / 2, iconBtnSize, iconBtnSize);
            entry.Button.AccessibleName = Strings.Get(entry.TooltipKey);
            var action = entry.OnClick;
            entry.Button.Click += (_, _) => action();

            if (ReferenceEquals(entry.Button, _starBtn))
            {
                _toolTip.Attach(entry.Button, () => Strings.Get(_starStatusKey), null);
            }
            else if (ReferenceEquals(entry.Button, _copyDiagBtn))
            {
                _toolTip.Attach(entry.Button, () => Strings.Get(_diagCopied ? "DiagnosticsCopied" : "CopyDiagnostics"), null);
            }
            else
            {
                var key = entry.TooltipKey;
                _toolTip.Attach(entry.Button, () => Strings.Get(key), null);
            }

            _footerBar.Controls.Add(entry.Button);
            rightX -= iconBtnSize + iconGap;
        }
        _donateBtn.IconColorOverride = Theme.DonateHeart;
    }

    private static Panel BuildSectionHeader(int x, int y, int width, int height, string text)
    {
        var panel = new Panel
        {
            Bounds = new Rectangle(x, y, width, height),
            BackColor = Color.Transparent,
        };
        panel.Paint += (_, e) =>
        {
            ThemePainter.PrepareHighQuality(e.Graphics);
            using var font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
            var caption = text.ToUpperInvariant();
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var size = TextRenderer.MeasureText(e.Graphics, caption, font, new Size(int.MaxValue, int.MaxValue), flags);
            var midY = panel.Height / 2;
            var textX = (panel.Width - size.Width) / 2;
            var textY = (panel.Height - size.Height) / 2;
            using var pen = new Pen(Color.FromArgb(90, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
            if (textX > 10)
            {
                e.Graphics.DrawLine(pen, 0, midY, textX - 8, midY);
                e.Graphics.DrawLine(pen, textX + size.Width + 8, midY, panel.Width, midY);
            }
            TextRenderer.DrawText(e.Graphics, caption, font, new Point(textX, textY), Theme.Accent, flags);
        };
        return panel;
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_checking || IsDisposed)
        {
            return;
        }

        _checking = true;
        _availableReleaseUrl = null;
        _checkUpdatesBtn.Enabled = false;
        _statusLabel.ForeColor = Theme.TextMuted;
        _statusLabel.Cursor = Cursors.Default;
        _statusLabel.Text = Strings.Get("UpdateChecking");
        _statusLabel.Visible = true;

        _checkCts?.Cancel();
        _checkCts?.Dispose();
        _checkCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = _checkCts.Token;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            using var response = await SharedHttp.SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
            var tagName = doc.RootElement.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;

            if (IsDisposed)
            {
                return;
            }

            if (TryParseVersion(tagName, out var remoteVersion) &&
                TryParseVersion(FormatAppVersion(), out var localVersion) &&
                remoteVersion > localVersion)
            {
                _availableReleaseUrl = string.IsNullOrWhiteSpace(htmlUrl) ? $"{RepoUrl}/releases/latest" : htmlUrl;
                _statusLabel.ForeColor = Theme.Accent;
                _statusLabel.Cursor = Cursors.Hand;
                _statusLabel.Text = Strings.Format("UpdateAvailable", tagName!.TrimStart('v', 'V'));
            }
            else
            {
                _statusLabel.ForeColor = Theme.Accent;
                _statusLabel.Text = Strings.Get("UpdateLatest");
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                _statusLabel.ForeColor = Theme.TextMuted;
                _statusLabel.Text = Strings.Get("UpdateError");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            if (!IsDisposed)
            {
                _statusLabel.ForeColor = Theme.TextMuted;
                _statusLabel.Text = Strings.Get("UpdateError");
            }
        }
        finally
        {
            _checking = false;
            if (!IsDisposed)
            {
                _checkUpdatesBtn.Enabled = true;
            }
        }
    }

    private void CopyDiagnosticsToClipboard()
    {
        var lines = new[]
        {
            $"CyberPDF {FormatAppVersion()}",
            $".NET: {Environment.Version}",
            $"OS: {Environment.OSVersion.VersionString} ({RuntimeInformation.OSArchitecture})",
            $"Locale: {Strings.Current}",
            $"Theme: {Theme.CurrentTheme}",
            "Renderer: PDFium",
        };
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
            _diagCopied = true;
            _copyDiagBtn.Icon = IconKind.Check;
            _copyDiagBtn.Invalidate();
            _copiedTimer.Stop();
            _copiedTimer.Start();
        }
        catch (ExternalException)
        {
            // Clipboard held by another process; skip without interrupting.
        }
    }

    private static List<SuiteAppEntry> PickRandomSuiteApps(int count)
    {
        var pool = (SuiteAppEntry[])SuitePool.Clone();
        Random.Shared.Shuffle(pool);
        var list = new List<SuiteAppEntry>(Math.Min(count, pool.Length));
        for (var i = 0; i < Math.Min(count, pool.Length); i++)
        {
            list.Add(pool[i]);
        }
        return list;
    }

    private static string FormatAppVersion()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver is null
            ? "0.3.1"
            : string.Create(CultureInfo.InvariantCulture, $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}");
    }

    private static bool TryParseVersion(string? raw, out Version parsed)
    {
        parsed = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        var clean = raw.Trim().TrimStart('v', 'V');
        var dash = clean.IndexOf('-');
        if (dash >= 0)
        {
            clean = clean.Substring(0, dash);
        }
        return Version.TryParse(clean, out var result) && (parsed = result) is not null;
    }

    private static Image? LoadEmbeddedImage(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return Image.FromStream(copy);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (SystemException ex)
        {
            Core.Diagnostics.AppLog.Warn($"OpenUrl failed for {url}: {ex.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"CyberPDF/{FormatAppVersion()}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        ThemePainter.PrepareHighQuality(g);

        // Soft ambient radial glow at the top matching CyberNotes's About dialog.
        var glowRect = new Rectangle(-ClientSize.Width / 4, -ClientSize.Height / 4, ClientSize.Width, ClientSize.Height / 2);
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(glowRect);
            using var pgb = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(Theme.IsDark ? 36 : 28, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B),
                SurroundColors = [Color.Transparent],
            };
            g.FillPath(pgb, path);
        }

        using var border = new Pen(Theme.BorderStrong);
        g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

        var highlightMargin = ClientSize.Width / 10;
        using var hairline = new Pen(Color.FromArgb(100, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
        g.DrawLine(hairline, highlightMargin, 1, ClientSize.Width - highlightMargin, 1);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Escape or Keys.Enter)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void DragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, 2, IntPtr.Zero);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _checkCts?.Cancel();
            _checkCts?.Dispose();
            _copiedTimer.Dispose();
            _toolTip.Dispose();
            foreach (var img in _ownedImages)
            {
                img.Dispose();
            }
            _ownedImages.Clear();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
}
