namespace CyberPDF.Core.Documents;

/// <summary>
/// Saves a byte-exact copy of an open PDF file without interrupting streaming
/// reads, using a sibling temporary file and atomic rename.
/// </summary>
public static class DocumentSaver
{
    private const int CopyBufferSize = 81920;

    /// <summary>
    /// Copies <paramref name="sourcePath"/> to <paramref name="destinationPath"/>.
    /// Returns <see langword="false"/> when both resolve to the same file, so
    /// saving onto the currently open path never truncates it.
    /// </summary>
    public static bool SaveCopy(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source PDF file was not found.", sourcePath);
        }

        var fullSource = Path.GetFullPath(sourcePath);
        var fullDestination = Path.GetFullPath(destinationPath);
        if (string.Equals(fullSource, fullDestination, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var destinationDirectory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tempPath = $"{fullDestination}.tmp.{Guid.NewGuid():N}";
        try
        {
            using (var input = new FileStream(fullSource, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output, CopyBufferSize);
                output.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullDestination, overwrite: true);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best-effort cleanup of incomplete temp output.
            }
            throw;
        }
    }
}
