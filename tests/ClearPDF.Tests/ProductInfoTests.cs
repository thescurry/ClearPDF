using ClearPDF;
using Xunit;

namespace ClearPDF.Tests;

public class ProductInfoTests
{
    [Fact]
    public void Version_is_1_2_0()
    {
        Assert.Equal("1.2.0", ProductInfo.Version);
        Assert.Equal("ClearPDF 1.2.0", ProductInfo.About);
    }
}
