namespace CyberPDF.Common.Settings;

/// <summary>
/// File-system locations used by the app. Everything lives under the user's
/// local application data folder, so installing or updating never needs
/// administrator rights and an uninstall can remove the whole tree.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "CyberPDF";

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>
    /// Directory that holds pdfium.dll at runtime. In development it is the
    /// fetched native folder; in a published build the DLL sits next to the exe.
    /// </summary>
    public static IEnumerable<string> NativeLibraryProbes()
    {
        yield return AppContext.BaseDirectory;

        // Development layout: <repo>/native/pdfium/bin, five levels up from
        // src/CyberPDF.UI/bin/<config>/<tfm>/.
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && current is not null; i++)
        {
            var candidate = Path.Combine(current.FullName, "native", "pdfium", "bin");
            if (Directory.Exists(candidate))
            {
                yield return candidate;
            }
            current = current.Parent;
        }
    }
}
