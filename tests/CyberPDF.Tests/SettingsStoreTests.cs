using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using Xunit;

namespace CyberPDF.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _file;

    public SettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "cyberpdf-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _file = Path.Combine(_directory, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A locked temp file must not fail the suite.
        }
    }

    [Fact]
    public void MissingFileYieldsDefaults()
    {
        var store = new SettingsStore(_file);
        var settings = store.Load();

        Assert.Equal(AppTheme.Default, settings.Theme);
        Assert.True(settings.RememberPagePosition);
        Assert.Empty(settings.RecentFiles);
        Assert.Equal(64, settings.RenderCacheBudgetMb);
    }

    [Fact]
    public void RoundTripPreservesValues()
    {
        var store = new SettingsStore(_file);
        var settings = new AppSettings
        {
            Theme = AppTheme.Light,
            Language = "en",
            RenderCacheBudgetMb = 256,
            RecentFiles = { @"C:\docs\a.pdf", @"C:\docs\b.pdf" },
        };
        settings.ViewStates[@"C:\docs\a.pdf"] = new DocumentViewState { Page = 7, Zoom = 1.5f, Rotation = 90 };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(AppTheme.Light, loaded.Theme);
        Assert.Equal("en", loaded.Language);
        Assert.Equal(256, loaded.RenderCacheBudgetMb);
        Assert.Equal(2, loaded.RecentFiles.Count);
        Assert.Equal(7, loaded.ViewStates[@"C:\docs\a.pdf"].Page);
        Assert.Equal(1.5f, loaded.ViewStates[@"C:\docs\a.pdf"].Zoom);
        Assert.Equal(90, loaded.ViewStates[@"C:\docs\a.pdf"].Rotation);
    }

    [Fact]
    public void AccentedPathsSurviveTheRoundTrip()
    {
        // Spanish file names must not come back escaped or mangled.
        var store = new SettingsStore(_file);
        var settings = new AppSettings();
        settings.RecentFiles.Add(@"C:\Users\Carlos\Documentos\Presupuesto año 2026.pdf");
        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal(@"C:\Users\Carlos\Documentos\Presupuesto año 2026.pdf", loaded.RecentFiles[0]);
    }

    [Fact]
    public void CorruptFileFallsBackToDefaultsInsteadOfThrowing()
    {
        File.WriteAllText(_file, "{ this is not json ");
        var settings = new SettingsStore(_file).Load();

        Assert.Equal(AppTheme.Default, settings.Theme);
    }

    [Fact]
    public void EmptyFileFallsBackToDefaults()
    {
        File.WriteAllText(_file, "   ");
        var settings = new SettingsStore(_file).Load();

        Assert.NotNull(settings.RecentFiles);
    }

    [Theory]
    [InlineData(0, 64)]
    [InlineData(4, 64)]
    [InlineData(99999, 64)]
    [InlineData(512, 512)]
    public void CacheBudgetIsSanitized(int stored, int expected)
    {
        File.WriteAllText(_file, $"{{\"renderCacheBudgetMb\":{stored}}}");
        var settings = new SettingsStore(_file).Load();

        Assert.Equal(expected, settings.RenderCacheBudgetMb);
    }

    [Fact]
    public void RecentFilesAreCapped()
    {
        var store = new SettingsStore(_file);
        var settings = new AppSettings();
        for (var i = 0; i < AppSettings.MaxRecentFiles + 20; i++)
        {
            settings.RecentFiles.Add($@"C:\docs\{i}.pdf");
        }
        store.Save(settings);

        var loaded = store.Load();
        Assert.Equal(AppSettings.MaxRecentFiles, loaded.RecentFiles.Count);
    }

    [Theory]
    [InlineData("es", Lang.Es)]
    [InlineData("EN", Lang.En)]
    [InlineData("english", Lang.En)]
    [InlineData("español", Lang.Es)]
    public void ExplicitLanguageIsResolved(string stored, Lang expected)
    {
        var settings = new AppSettings { Language = stored };
        Assert.Equal(expected, SettingsStore.ResolveLanguage(settings));
    }

    [Fact]
    public void SidePanelStateSurvivesTheRoundTrip()
    {
        var store = new SettingsStore(_file);
        store.Save(new AppSettings { SidePanelOpen = true, SidePanelTab = 1, SidePanelWidth = 240 });

        var loaded = store.Load();

        Assert.True(loaded.SidePanelOpen);
        Assert.Equal(1, loaded.SidePanelTab);
        Assert.Equal(240, loaded.SidePanelWidth);
    }

    [Theory]
    [InlineData(0, 190)]
    [InlineData(40, 190)]
    [InlineData(9000, 190)]
    [InlineData(300, 300)]
    public void SidePanelWidthOutsideTheRangeFallsBack(int stored, int expected)
    {
        File.WriteAllText(_file, $"{{\"sidePanelWidth\":{stored}}}");
        var settings = new SettingsStore(_file).Load();

        Assert.Equal(expected, settings.SidePanelWidth);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(7, 0)]
    [InlineData(1, 1)]
    public void SidePanelTabOutsideTheRangeFallsBackToPages(int stored, int expected)
    {
        File.WriteAllText(_file, $"{{\"sidePanelTab\":{stored}}}");
        var settings = new SettingsStore(_file).Load();

        Assert.Equal(expected, settings.SidePanelTab);
    }

    [Fact]
    public void PerDocumentPanelStateSurvivesAndStaysDistinct()
    {
        // The point of the per-document field: one document can keep the panel
        // closed while another keeps it open, with the global flag untouched.
        var store = new SettingsStore(_file);
        var settings = new AppSettings { SidePanelOpen = true };
        settings.ViewStates[@"C:\docs\long.pdf"] = new DocumentViewState { SidePanelOpen = false, SidePanelTab = 0 };
        settings.ViewStates[@"C:\docs\outline.pdf"] = new DocumentViewState { SidePanelOpen = true, SidePanelTab = 1 };
        store.Save(settings);

        var loaded = store.Load();

        Assert.True(loaded.SidePanelOpen);
        Assert.False(loaded.ViewStates[@"C:\docs\long.pdf"].SidePanelOpen);
        Assert.True(loaded.ViewStates[@"C:\docs\outline.pdf"].SidePanelOpen);
        Assert.Equal(1, loaded.ViewStates[@"C:\docs\outline.pdf"].SidePanelTab);
    }

    [Fact]
    public void UnsetPerDocumentPanelStateStaysNull()
    {
        // Null is not "closed": it means the document never chose, so the global
        // preference must apply. A default of false would silently close the
        // panel for every document saved before this field existed.
        var store = new SettingsStore(_file);
        var settings = new AppSettings();
        settings.ViewStates[@"C:\docs\plain.pdf"] = new DocumentViewState { Page = 3 };
        store.Save(settings);

        var loaded = store.Load();

        Assert.Null(loaded.ViewStates[@"C:\docs\plain.pdf"].SidePanelOpen);
        Assert.Null(loaded.ViewStates[@"C:\docs\plain.pdf"].SidePanelTab);
    }

    [Fact]
    public void SaveCreatesTheDirectoryWhenMissing()
    {
        var nested = Path.Combine(_directory, "nested", "deeper", "settings.json");
        var store = new SettingsStore(nested);
        store.Save(new AppSettings { Theme = AppTheme.Gray });

        Assert.True(File.Exists(nested));
    }
}
