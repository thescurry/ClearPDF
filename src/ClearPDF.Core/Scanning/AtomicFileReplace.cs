namespace ClearPDF.Scanning;

/// <summary>
/// Write a complete file to a sibling temp path, then replace the destination.
/// Never streams bytes onto an existing target in place (so an open PDF cannot
/// be truncated mid-write).
/// </summary>
public static class AtomicFileReplace
{
    public static void Write(string destinationPath, Action<string> writeTempFile)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        ArgumentNullException.ThrowIfNull(writeTempFile);

        var dest = Path.GetFullPath(destinationPath);
        var dir = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = Path.Combine(
            string.IsNullOrEmpty(dir) ? Path.GetTempPath() : dir,
            $".{Path.GetFileName(dest)}.{Guid.NewGuid():N}.tmp");

        try
        {
            writeTempFile(tmp);
            if (!File.Exists(tmp) || new FileInfo(tmp).Length == 0)
                throw new IOException("PDF write produced no file.");

            if (File.Exists(dest))
                ReplaceExisting(tmp, dest);
            else
                File.Move(tmp, dest);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); }
                catch (IOException) { /* leftover temp is harmless */ }
            }
        }
    }

    private static void ReplaceExisting(string tmp, string dest)
    {
        try
        {
            File.Replace(tmp, dest, destinationBackupFileName: null);
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            File.Delete(dest);
            File.Move(tmp, dest);
        }
    }
}
