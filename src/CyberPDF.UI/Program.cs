using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.Core.Diagnostics;
using CyberPDF.Core.Rendering;
using CyberPDF.UI.Services;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI;

/// <summary>
/// Application entry point. Deliberately minimal: no splash screen, no updater
/// check on the startup path, nothing that could delay the first frame. The
/// window is created before the PDF engine so the user sees the UI immediately
/// while the engine thread spins up.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // One process per session. A second launch (a double-clicked document)
        // forwards the file to the running window instead of starting a copy.
        using var singleInstance = new SingleInstance();
        var requestedFile = args.FirstOrDefault(a => !a.StartsWith('-') && File.Exists(a));

        if (!singleInstance.IsFirstInstance)
        {
            singleInstance.SendToRunningInstance(requestedFile);
            // A reader must never leave a headless process behind: without an
            // explicit exit the WinForms runtime keeps running with no window.
            Environment.Exit(0);
            return;
        }

        var store = new SettingsStore();
        var settings = store.Load();
        Strings.Current = SettingsStore.ResolveLanguage(settings);
        Theme.Apply(settings.Theme);

        var budget = (long)settings.RenderCacheBudgetMb * 1024 * 1024;
        using var engine = new PdfEngine(budget);

        using var window = new MainWindow(engine, settings, store);

        // Opening from the command line (file association, "Open with", drag onto
        // the exe) must show the first page as soon as possible, so it is queued
        // before the message loop starts and the window appears over it.
        var diag = DiagnosticsProbe.IsRequested(args);
        var shotPath = ScreenshotProbe.OutputPath(args);
        var initial = diag
            ? DiagnosticsProbe.TargetFile(args)
            : shotPath is not null
                ? ScreenshotProbe.TargetFile(args)
                : requestedFile;

        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            window.Shown += async (_, _) =>
            {
                await Task.Delay(2500);
                await window.RunSelectionSelfTestAsync();
                window.Close();
            };
        }

        if (diag)
        {
            var stress = args.Any(a => a.Equals("--stress", StringComparison.OrdinalIgnoreCase));
            window.RequestDiagnostics(stress);
        }
        if (shotPath is not null)
        {
            var withSelection = args.Any(a => a.Equals("--select", StringComparison.OrdinalIgnoreCase));
            window.Shown += async (_, _) =>
            {
                await ScreenshotProbe.CaptureAsync(window, shotPath, withSelection);
                window.Close();
            };
        }

        window.Shown += (_, _) =>
        {
            if (initial is not null)
            {
                window.OpenDocument(initial);
            }

            // From now on, later launches deliver their file through the mutex
            // owner instead of starting a second process.
            window.AttachSingleInstance(singleInstance);
            singleInstance.Listen(
                window,
                path =>
                {
                    window.OpenDocument(path);
                    window.Activate();
                });
        };

        try
        {
            Application.Run(window);
        }
        catch (Exception ex)
        {
            AppLog.Error("Unhandled exception at the message loop", ex);
            MessageBox.Show(
                $"{Strings.Get("ErrorTitle")}{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                Strings.Get("AppTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
