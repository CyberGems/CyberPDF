namespace CyberPDF.Common.Shell;

/// <summary>
/// Pure helper methods for building and matching Windows <c>.pdf</c> shell
/// association values so formatting and matching rules can be unit-tested
/// without touching the live user registry.
/// </summary>
public static class PdfAssociationHelper
{
    public const string ProgId = "CyberPDF.Document";

    public static string NormalizeExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim().Trim('"');
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }

    public static string BuildOpenCommand(string executablePath)
        => $"\"{NormalizeExecutablePath(executablePath)}\" \"%1\"";

    public static string BuildDefaultIconValue(string executablePath)
        => $"\"{NormalizeExecutablePath(executablePath)}\",0";

    public static bool MatchesDefaultHandler(
        string? queriedProgId,
        string? queriedExe,
        string? userChoiceProgId,
        string? queriedCommand,
        string? currentExePath)
    {
        if (string.Equals(queriedProgId?.Trim(), ProgId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var currentExe = NormalizeExecutablePath(currentExePath);
        var associatedExe = NormalizeExecutablePath(queriedExe);
        if (!string.IsNullOrEmpty(currentExe) &&
            string.Equals(associatedExe, currentExe, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(userChoiceProgId))
        {
            return string.Equals(userChoiceProgId.Trim(), ProgId, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(currentExe) &&
            !string.IsNullOrEmpty(queriedCommand) &&
            queriedCommand.Contains(currentExe, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
