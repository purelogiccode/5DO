using FiveDo.Frontend.Avalonia.Views;
using FiveDo.Input;

namespace FiveDo.Tests.Frontend;

public sealed class MousePadMappingTests
{
    [Fact]
    public void CenterAndDeadZone_ProduceNoDirection()
    {
        Assert.Equal(ControllerButton.None, MainWindow.MapMousePad(0, 0, false, false, false));
        Assert.Equal(ControllerButton.None, MainWindow.MapMousePad(0.1, -0.1, false, false, false));
    }

    [Theory]
    [InlineData(0.4, 0, ControllerButton.Right)]
    [InlineData(-0.4, 0, ControllerButton.Left)]
    [InlineData(0, 0.4, ControllerButton.Down)]
    [InlineData(0, -0.4, ControllerButton.Up)]
    [InlineData(0.4, -0.4, ControllerButton.Right | ControllerButton.Up)]
    public void Directions_MapFromThePointerPosition(
        double x,
        double y,
        ControllerButton expected)
    {
        Assert.Equal(expected, MainWindow.MapMousePad(x, y, false, false, false));
    }

    [Fact]
    public void Buttons_MapToAbc()
    {
        Assert.Equal(ControllerButton.A, MainWindow.MapMousePad(0, 0, true, false, false));
        Assert.Equal(ControllerButton.B, MainWindow.MapMousePad(0, 0, false, true, false));
        Assert.Equal(ControllerButton.C, MainWindow.MapMousePad(0, 0, false, false, true));
    }
}
