using ClearPDF;
using Xunit;

namespace ClearPDF.Tests;

public class ProductInfoTests
{
    [Fact]
    public void Version_is_1_1_0()
    {
        Assert.Equal("1.1.0", ProductInfo.Version);
        Assert.Equal("ClearPDF 1.1.0", ProductInfo.About);
    }
}
