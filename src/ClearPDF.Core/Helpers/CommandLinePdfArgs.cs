namespace ClearPDF.Helpers;

/// <summary>
/// Picks the first existing PDF path from command-line style arguments.
/// UI-free so it can be unit-tested on Linux.
/// </summary>
public static class CommandLinePdfArgs
{
    /// <summary>
    /// Returns the first argument that looks like an existing <c>.pdf</c> file path,
    /// or <c>null</c> if none. Ignores blank/junk args. Relative paths are resolved
    /// against the current working directory. Surrounding quotes are stripped if present
    /// (runtime usually unquotes already; this covers edge cases).
    /// </summary>
    /// <param name="args">
    /// Argument list <em>without</em> the executable (argv[0]). Pass
    /// <c>Environment.GetCommandLineArgs().Skip(1)</c> from the WPF shell.
    /// </param>
    public static string? TryGetFirstExistingPdf(IEnumerable<string>? args)
    {
        if (args is null)
            return null;

        foreach (var raw in args)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var path = UnwrapQuotes(raw.Trim());
            if (path.Length == 0)
                continue;

            if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                continue;

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch
            {
                // Invalid path characters / empty after normalize — skip.
                continue;
            }

            try
            {
                if (File.Exists(full))
                    return full;
            }
            catch
            {
                // SecurityException / IOException probing path — skip.
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Convenience: read process command line, skip argv[0] (exe), return first PDF path.
    /// </summary>
    public static string? TryGetPdfFromProcessCommandLine()
    {
        var all = Environment.GetCommandLineArgs();
        return TryGetFirstExistingPdf(all.Skip(1));
    }

    private static string UnwrapQuotes(string s)
    {
        if (s.Length >= 2)
        {
            var a = s[0];
            var b = s[^1];
            if ((a == '"' && b == '"') || (a == '\'' && b == '\''))
                return s[1..^1].Trim();
        }
        return s;
    }
}
