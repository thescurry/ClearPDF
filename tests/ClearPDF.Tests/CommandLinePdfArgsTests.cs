using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class CommandLinePdfArgsTests : IDisposable
{
    private readonly string _dir;
    private readonly string _pdfPath;
    private readonly string _txtPath;
    private readonly string _missingPdf;

    public CommandLinePdfArgsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ClearPDF-cli-args-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _pdfPath = Path.Combine(_dir, "sample doc.pdf"); // space in name
        _txtPath = Path.Combine(_dir, "notes.txt");
        _missingPdf = Path.Combine(_dir, "gone.pdf");
        File.WriteAllText(_pdfPath, "%PDF-1.4 fake");
        File.WriteAllText(_txtPath, "not a pdf");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void Null_or_empty_returns_null()
    {
        Assert.Null(CommandLinePdfArgs.TryGetFirstExistingPdf(null));
        Assert.Null(CommandLinePdfArgs.TryGetFirstExistingPdf(Array.Empty<string>()));
        Assert.Null(CommandLinePdfArgs.TryGetFirstExistingPdf(new[] { "", "  " }));
    }

    [Fact]
    public void Picks_first_existing_pdf_ignores_junk()
    {
        var result = CommandLinePdfArgs.TryGetFirstExistingPdf(new[]
        {
            "--verbose",
            _txtPath,
            _missingPdf,
            _pdfPath,
            Path.Combine(_dir, "other.pdf")
        });
        Assert.Equal(Path.GetFullPath(_pdfPath), result);
    }

    [Fact]
    public void Accepts_quoted_path_with_spaces()
    {
        var quoted = "\"" + _pdfPath + "\"";
        var result = CommandLinePdfArgs.TryGetFirstExistingPdf(new[] { quoted });
        Assert.Equal(Path.GetFullPath(_pdfPath), result);
    }

    [Fact]
    public void Extension_match_is_case_insensitive()
    {
        var upper = Path.Combine(_dir, "UPPER.PDF");
        File.WriteAllText(upper, "%PDF");
        var result = CommandLinePdfArgs.TryGetFirstExistingPdf(new[] { upper });
        Assert.Equal(Path.GetFullPath(upper), result);
    }

    [Fact]
    public void Relative_path_resolves_when_cwd_allows()
    {
        var prev = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_dir);
            var result = CommandLinePdfArgs.TryGetFirstExistingPdf(new[] { "sample doc.pdf" });
            Assert.Equal(Path.GetFullPath(_pdfPath), result);
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
        }
    }

    [Fact]
    public void Non_pdf_extension_ignored_even_if_exists()
    {
        Assert.Null(CommandLinePdfArgs.TryGetFirstExistingPdf(new[] { _txtPath }));
    }

    [Fact]
    public void Open_with_style_argv_skips_exe_and_picks_pdf()
    {
        // Explorer "Open with" / `ClearPDF.exe "file.pdf"` — argv[0] is the exe.
        var argv = new[] { @"C:\Program Files\ClearPDF\ClearPDF.exe", _pdfPath };
        var result = CommandLinePdfArgs.TryGetFirstExistingPdf(argv.Skip(1));
        Assert.Equal(Path.GetFullPath(_pdfPath), result);
    }
}
