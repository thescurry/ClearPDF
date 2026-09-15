using ClearPDF.Helpers;
using Xunit;

namespace ClearPDF.Tests;

public class PdfPageExtractTests
{
    [Fact]
    public void Normalize_sorts_uniques_and_drops_out_of_range()
    {
        var pages = PdfPageExtract.Normalize(new[] { 5, 1, 5, -1, 1, 99, 0 }, pageCount: 6);
        Assert.Equal(new[] { 0, 1, 5 }, pages);
    }

    [Fact]
    public void Normalize_empty_or_zero_count()
    {
        Assert.Empty(PdfPageExtract.Normalize(null, 4));
        Assert.Empty(PdfPageExtract.Normalize(new[] { 0 }, 0));
        Assert.Empty(PdfPageExtract.Normalize(Array.Empty<int>(), 3));
    }

    [Fact]
    public void PagesForExtract_uses_selection_when_present()
    {
        var pages = PdfPageExtract.PagesForExtract(new[] { 2, 0 }, currentPage0: 7, pageCount: 10);
        Assert.Equal(new[] { 0, 2 }, pages);
    }

    [Fact]
    public void PagesForExtract_falls_back_to_current_page()
    {
        var pages = PdfPageExtract.PagesForExtract(Array.Empty<int>(), currentPage0: 4, pageCount: 8);
        Assert.Equal(new[] { 4 }, pages);
    }

    [Fact]
    public void ShouldOfferSaveAsChoice_only_when_multi()
    {
        Assert.False(PdfPageExtract.ShouldOfferSaveAsChoice(0));
        Assert.False(PdfPageExtract.ShouldOfferSaveAsChoice(1));
        Assert.True(PdfPageExtract.ShouldOfferSaveAsChoice(2));
        Assert.True(PdfPageExtract.ShouldOfferSaveAsChoice(8));
    }

    [Fact]
    public void ToDocnetPageRange_is_1_based_with_runs()
    {
        Assert.Equal(string.Empty, PdfPageExtract.ToDocnetPageRange(Array.Empty<int>()));
        Assert.Equal("3", PdfPageExtract.ToDocnetPageRange(new[] { 2 }));
        Assert.Equal("1-3", PdfPageExtract.ToDocnetPageRange(new[] { 0, 1, 2 }));
        Assert.Equal("1,3,5-7", PdfPageExtract.ToDocnetPageRange(new[] { 0, 2, 4, 5, 6 }));
        Assert.Equal("2-3,6", PdfPageExtract.ToDocnetPageRange(new[] { 1, 2, 5 }));
    }

    [Fact]
    public void SuggestFileName_single_range_and_sparse()
    {
        Assert.Equal("report-page-3.pdf",
            PdfPageExtract.SuggestFileName(@"C:\docs\report.pdf", new[] { 2 }));
        Assert.Equal("report-pages-2-5.pdf",
            PdfPageExtract.SuggestFileName("report.pdf", new[] { 1, 2, 3, 4 }));
        Assert.Equal("report-pages-1-3-7.pdf",
            PdfPageExtract.SuggestFileName("report.pdf", new[] { 0, 2, 6 }));
        Assert.Equal("deck-pages-2-x6.pdf",
            PdfPageExtract.SuggestFileName("deck.pdf", new[] { 1, 2, 4, 5, 7, 8 }));
        Assert.Equal("document-page-1.pdf",
            PdfPageExtract.SuggestFileName("", new[] { 0 }));
    }
}
