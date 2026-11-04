using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class DocumentPathTests
{
    [Fact]
    public void Same_aliases_only_private_var_tmp_etc()
    {
        Assert.True(DocumentPath.Same("/var/tmp/a.pdf", "/private/var/tmp/a.pdf"));
        Assert.True(DocumentPath.Same("/tmp/a.pdf", "/private/tmp/a.pdf"));
        Assert.True(DocumentPath.Same("/etc/a.pdf", "/private/etc/a.pdf"));
        Assert.False(DocumentPath.Same("/Users/x/a.pdf", "/private/Users/x/a.pdf"));
        Assert.False(DocumentPath.Same("/var/tmp/a.pdf", "/var/tmp/b.pdf"));
    }

    [Fact]
    public void Same_is_case_sensitive_on_posix()
    {
        Assert.False(DocumentPath.Same("/var/tmp/A.pdf", "/var/tmp/a.pdf"));
        Assert.False(DocumentPath.Same("/Private/var/tmp/a.pdf", "/var/tmp/a.pdf"));
    }

    [Fact]
    public void Same_collapses_dot_dot()
    {
        Assert.True(DocumentPath.Same("/var/tmp/../tmp/a.pdf", "/var/tmp/a.pdf"));
        Assert.True(DocumentPath.Same("/private/var/tmp/../tmp/a.pdf", "/var/tmp/a.pdf"));
    }

    [Fact]
    public void Same_does_not_let_windows_GetFullPath_split_posix_aliases()
    {
        Assert.Equal(
            DocumentPath.Normalize("/var/tmp/doc.pdf"),
            DocumentPath.Normalize("/private/var/tmp/doc.pdf"));
    }

    [Fact]
    public void Same_windows_paths_are_case_insensitive_and_normalize_drive()
    {
        Assert.True(DocumentPath.Same(@"C:\Docs\A.pdf", @"c:\docs\a.pdf"));
        Assert.True(DocumentPath.Same(@"C:\Docs\foo\..\A.pdf", @"c:\docs\a.pdf"));
        Assert.False(DocumentPath.Same(@"C:\Docs\A.pdf", @"C:\Docs\B.pdf"));
    }

    [SkippableFact]
    public void Same_resolves_symlink_when_file_exists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ClearPDF-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var real = Path.Combine(dir, "real.pdf");
        var link = Path.Combine(dir, "link.pdf");
        File.WriteAllText(real, "%PDF");
        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception ex)
        {
            TryCleanup(dir);
            throw new SkipException("Could not create symlink: " + ex.Message);
        }

        try
        {
            Skip.IfNot(File.Exists(link), "Created symlink is not visible as a file.");
            Assert.True(DocumentPath.Same(real, link));
        }
        finally
        {
            TryCleanup(dir);
        }
    }

    private static void TryCleanup(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }
}
