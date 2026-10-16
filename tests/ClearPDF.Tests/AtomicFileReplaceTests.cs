using ClearPDF.Scanning;
using Xunit;

namespace ClearPDF.Tests;

public class AtomicFileReplaceTests : IDisposable
{
    private readonly string _dir;

    public AtomicFileReplaceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "clearpdf-atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        AtomicFileReplace.Relocate = AtomicFileReplace.MoveOverwrite;
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void Failed_move_leaves_original_pdf_unchanged()
    {
        var dest = Path.Combine(_dir, "original.pdf");
        var original = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x01, 0x02, 0x03 };
        File.WriteAllBytes(dest, original);

        AtomicFileReplace.Relocate = (_, _) =>
            throw new IOException("forced relocate failure");

        var ex = Assert.Throws<IOException>(() =>
            AtomicFileReplace.Write(dest, tmp => File.WriteAllBytes(tmp, new byte[] { 0xFF, 0xFF, 0xFF })));

        Assert.Contains("forced relocate", ex.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(dest));
        Assert.Equal(original, File.ReadAllBytes(dest));
        Assert.Empty(Directory.GetFiles(_dir, ".original.pdf.*.tmp"));
    }

    [Fact]
    public void Overwrite_move_replaces_destination_and_drops_temp()
    {
        var dest = Path.Combine(_dir, "doc.pdf");
        File.WriteAllBytes(dest, new byte[] { 1, 2, 3 });
        var next = new byte[] { 9, 8, 7, 6 };

        AtomicFileReplace.Write(dest, tmp => File.WriteAllBytes(tmp, next));

        Assert.Equal(next, File.ReadAllBytes(dest));
        Assert.Empty(Directory.GetFiles(_dir, ".doc.pdf.*.tmp"));
    }
}
