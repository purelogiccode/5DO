using System;
using FiveDo.Video.OpenGL;

namespace FiveDo.Tests.Video;

public sealed class OffscreenRenderTargetTests
{
    [Fact]
    public void Create_RejectsANullGlApi()
    {
        Assert.Throws<ArgumentNullException>(() => OffscreenRenderTarget.Create(null!, 16, 16));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(16, 0)]
    [InlineData(-1, 16)]
    [InlineData(16, -1)]
    public void Create_RejectsNonPositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OffscreenRenderTarget.Create(null!, width, height));
    }
}
