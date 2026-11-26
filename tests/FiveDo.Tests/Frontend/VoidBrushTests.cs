using Avalonia.Media;
using FiveDo.Frontend.Avalonia.Views;
using FiveDo.Host;
using FiveDo.Video;

namespace FiveDo.Tests.Frontend;

public sealed class VoidBrushTests
{
    [Fact]
    public void Solid_ReturnsASolidColorBrush()
    {
        IBrush brush = MainWindow.CreateVoidBrush(Colors.Black, VoidPattern.Solid);

        var solid = Assert.IsType<SolidColorBrush>(brush);
        Assert.Equal(Colors.Black, solid.Color);
    }

    [Theory]
    [InlineData(VoidPattern.Checker)]
    [InlineData(VoidPattern.Grid)]
    [InlineData(VoidPattern.Scanline)]
    [InlineData(VoidPattern.FourDO)]
    [InlineData(VoidPattern.Bumps)]
    [InlineData(VoidPattern.Metal)]
    public void Patterns_ReturnATiledDrawingBrush(VoidPattern pattern)
    {
        IBrush brush = MainWindow.CreateVoidBrush(Colors.Black, pattern);

        var drawing = Assert.IsType<DrawingBrush>(brush);
        Assert.Equal(TileMode.Tile, drawing.TileMode);
        Assert.True(drawing.DestinationRect.Rect.Width > 0);
        Assert.NotNull(drawing.Drawing);
    }

    [Fact]
    public void FourDo_UsesTheLargerTile()
    {
        var fourDo = Assert.IsType<DrawingBrush>(
            MainWindow.CreateVoidBrush(Colors.Black, VoidPattern.FourDO));
        var checker = Assert.IsType<DrawingBrush>(
            MainWindow.CreateVoidBrush(Colors.Black, VoidPattern.Checker));

        Assert.Equal(32, fourDo.DestinationRect.Rect.Width);
        Assert.Equal(16, checker.DestinationRect.Rect.Width);
    }
}
