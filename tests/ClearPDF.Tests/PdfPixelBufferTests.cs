using ClearPDF.Imaging;
using Xunit;

namespace ClearPDF.Tests;

public class PdfPixelBufferTests
{
    [Fact]
    public void Ctor_rejects_null_pixels()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfPixelBuffer(null!, 1, 1));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 10)]
    [InlineData(10, 0)]
    [InlineData(10, -4)]
    public void Ctor_rejects_non_positive_dimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfPixelBuffer(new byte[64], width, height));
    }

    [Fact]
    public void Default_stride_is_width_times_four()
    {
        var pixels = new byte[2 * 3 * 4];
        var buffer = new PdfPixelBuffer(pixels, 2, 3);

        Assert.Equal(8, buffer.Stride);
        Assert.Same(pixels, buffer.Bgra32);
        Assert.Equal(2, buffer.Width);
        Assert.Equal(3, buffer.Height);
        Assert.False(buffer.IsEmpty);
    }

    [Fact]
    public void Ctor_rejects_buffer_shorter_than_stride_times_height()
    {
        Assert.Throws<ArgumentException>(() =>
            new PdfPixelBuffer(new byte[11], 2, 2, stride: 8));
    }

    [Fact]
    public void Ctor_accepts_padded_stride()
    {
        var pixels = new byte[16 * 2];
        var buffer = new PdfPixelBuffer(pixels, 3, 2, stride: 16);

        Assert.Equal(16, buffer.Stride);
        Assert.Equal(3, buffer.Width);
        Assert.Equal(2, buffer.Height);
    }

    [Fact]
    public void CreatePlaceholder_fills_white_page_with_gray_edge()
    {
        var buffer = PdfPixelBuffer.CreatePlaceholder(4, 3);

        Assert.Equal(4, buffer.Width);
        Assert.Equal(3, buffer.Height);
        Assert.Equal(16, buffer.Stride);

        // Interior pixel (1,1) is white BGRA.
        var i = buffer.Stride + 4;
        Assert.Equal(0xFF, buffer.Bgra32[i]);
        Assert.Equal(0xFF, buffer.Bgra32[i + 1]);
        Assert.Equal(0xFF, buffer.Bgra32[i + 2]);
        Assert.Equal(0xFF, buffer.Bgra32[i + 3]);

        // Top-left edge is gray.
        Assert.Equal(0xCC, buffer.Bgra32[0]);
        Assert.Equal(0xCC, buffer.Bgra32[1]);
        Assert.Equal(0xCC, buffer.Bgra32[2]);
        Assert.Equal(0xFF, buffer.Bgra32[3]);
    }

    [Fact]
    public void CreatePlaceholder_rejects_non_positive_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPixelBuffer.CreatePlaceholder(0, 10));
    }
}
