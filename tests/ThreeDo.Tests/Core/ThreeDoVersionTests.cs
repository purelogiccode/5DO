namespace ThreeDo.Tests.Core;

public sealed class ThreeDoVersionTests
{
    [Fact]
    public void ProductName_IsExpected()
    {
        Assert.Equal("3DOEmulator", ThreeDo.Core.ThreeDoVersion.ProductName);
    }

    [Fact]
    public void NamespacePrefix_IsThreeDo()
    {
        Assert.Equal("ThreeDo", ThreeDo.Core.ThreeDoVersion.NamespacePrefix);
    }
}
