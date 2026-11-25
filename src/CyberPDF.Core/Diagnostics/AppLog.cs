namespace CyberPDF.Core.Diagnostics;

/// <summary>
/// Minimal append-only logger. Diagnostics are written to
/// %LOCALAPPDATA%\CyberPDF\logs\cyberpdf.log, capped in size, so a user can send
/// one file when reporting a problem. It must never throw: logging is optional.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly long MaxBytes = 2 * 1024 * 1024;
    private static string? _path;

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var path = EnsurePath();
                if (path is null)
                {
                    return;
                }

                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var backup = path + ".1";
                    File.Delete(backup);
                    File.Move(path, backup);
                }

                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Never let logging break the app.
        }
    }

    private static string? EnsurePath()
    {
        if (_path is not null)
        {
            return _path;
        }

        var directory = Common.Settings.AppPaths.LogDirectory;
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "cyberpdf.log");
        return _path;
    }
}
