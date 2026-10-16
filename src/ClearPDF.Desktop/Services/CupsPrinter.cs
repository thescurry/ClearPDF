using System.Diagnostics;
using ClearPDF.Helpers;

namespace ClearPDF.Desktop.Services;

/// <summary>
/// Sends a PDF to the default (or named) CUPS queue via <c>lp</c>/<c>lpr</c>.
/// Never opens an Apple ID session.
/// </summary>
public static class CupsPrinter
{
    public static bool IsAvailable() =>
        FindOnPath(CupsPrintArgs.LpExecutable) != null
        || FindOnPath(CupsPrintArgs.LprExecutable) != null;

    public static void PrintPdf(string pdfPath, string? printer = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new FileNotFoundException("PDF to print was not found.", pdfPath);

        var lp = FindOnPath(CupsPrintArgs.LpExecutable);
        if (lp != null)
        {
            Run(lp, CupsPrintArgs.BuildLp(pdfPath, printer));
            return;
        }

        var lpr = FindOnPath(CupsPrintArgs.LprExecutable);
        if (lpr != null)
        {
            Run(lpr, CupsPrintArgs.BuildLpr(pdfPath, printer));
            return;
        }

        throw new InvalidOperationException(
            "No system printer command (lp/lpr) is available.");
    }

    private static void Run(string fileName, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start {fileName}.");
        if (!proc.WaitForExit(30_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException($"{fileName} did not finish.");
        }

        if (proc.ExitCode != 0)
        {
            var err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"{fileName} failed ({proc.ExitCode}): {err}".Trim());
        }
    }

    private static string? FindOnPath(string name)
    {
        foreach (var candidate in new[]
        {
            Path.Combine("/usr/bin", name),
            Path.Combine("/bin", name),
            Path.Combine("/usr/sbin", name)
        })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
