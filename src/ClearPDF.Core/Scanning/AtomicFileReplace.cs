namespace ClearPDF.Scanning;

/// <summary>
/// Write a complete file to a sibling temp path, then replace the destination.
/// Never streams bytes onto an existing target in place (so an open PDF cannot
/// be truncated mid-write). Never deletes the destination first — a failed
/// relocate must leave the original PDF intact.
/// </summary>
public static class AtomicFileReplace
{
    /// <summary>
    /// Production relocate. Tests may replace this to force a move failure.
    /// </summary>
    internal static Action<string, string> Relocate { get; set; } = MoveOverwrite;

    internal static void MoveOverwrite(string source, string destination) =>
        File.Move(source, destination, overwrite: true);

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

            Relocate(tmp, dest);
        }
        finally
        {
            // Delete leftover temp only. Never delete dest here — if Relocate
            // failed, dest is still the original PDF.
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); }
                catch (IOException) { /* leftover temp is harmless */ }
            }
        }
    }
}
