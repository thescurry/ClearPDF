using System.IO;
using System.Text;

namespace ClearPDF.Services;

/// <summary>
/// Simple local file logger under %LocalAppData%\ClearPDF\logs\.
/// No network, no telemetry — short lines only.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static bool _dirReady;

    public static string LogDirectory
    {
        get
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClearPDF",
                "logs");
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    /// <summary>Optional once-per-start version line.</summary>
    public static void LogAppStart()
    {
        Info($"ClearPDF {global::ClearPDF.ProductInfo.Version} start");
    }

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            EnsureDirectory();
            var path = Path.Combine(LogDirectory, $"clearpdf-{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder(256);
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
            sb.Append(' ');
            sb.Append(level);
            sb.Append(' ');
            sb.Append(message ?? string.Empty);
            if (ex != null)
            {
                sb.Append(" | ");
                sb.Append(ex.GetType().Name);
                sb.Append(": ");
                sb.Append(ex.Message);
                sb.AppendLine();
                sb.Append(ex.StackTrace);
            }

            sb.AppendLine();
            lock (Gate)
            {
                File.AppendAllText(path, sb.ToString());
            }
        }
        catch
        {
            // Never throw from logger.
        }
    }

    private static void EnsureDirectory()
    {
        if (_dirReady)
            return;
        Directory.CreateDirectory(LogDirectory);
        _dirReady = true;
    }
}
