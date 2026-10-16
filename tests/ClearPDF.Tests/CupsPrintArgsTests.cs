using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class CupsPrintArgsTests
{
    [Fact]
    public void Lp_default_printer_uses_double_dash_then_path()
    {
        var args = CupsPrintArgs.BuildLp("/tmp/doc.pdf");
        Assert.Equal(new[] { "--", "/tmp/doc.pdf" }, args);
    }

    [Fact]
    public void Lp_named_printer_is_dash_d()
    {
        var args = CupsPrintArgs.BuildLp("/tmp/doc.pdf", "HP_ENVY");
        Assert.Equal(new[] { "-d", "HP_ENVY", "--", "/tmp/doc.pdf" }, args);
    }

    [Fact]
    public void Lpr_named_printer_is_dash_P()
    {
        var args = CupsPrintArgs.BuildLpr("/tmp/doc.pdf", "Office");
        Assert.Equal(new[] { "-P", "Office", "--", "/tmp/doc.pdf" }, args);
    }

    [Fact]
    public void Empty_path_throws()
    {
        Assert.Throws<ArgumentException>(() => CupsPrintArgs.BuildLp(" "));
        Assert.Throws<ArgumentException>(() => CupsPrintArgs.BuildLpr(""));
    }
}
