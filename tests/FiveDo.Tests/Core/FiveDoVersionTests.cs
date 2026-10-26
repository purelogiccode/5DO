namespace FiveDo.Tests.Core;

public sealed class FiveDoVersionTests
{
    [Fact]
    public void ProductName_IsExpected()
    {
        Assert.Equal("5DO", FiveDo.Core.FiveDoVersion.ProductName);
    }

    [Fact]
    public void NamespacePrefix_IsFiveDo()
    {
        Assert.Equal("FiveDo", FiveDo.Core.FiveDoVersion.NamespacePrefix);
    }
}
