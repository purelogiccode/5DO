using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class GamepadMenuChordTests
{
    [Theory]
    [InlineData(GamepadButtons.None, false)]
    [InlineData(GamepadButtons.Start, false)]
    [InlineData(GamepadButtons.Select, false)]
    [InlineData(GamepadButtons.Start | GamepadButtons.Select, true)]
    [InlineData(GamepadButtons.Start | GamepadButtons.Select | GamepadButtons.South, true)]
    public void IsPressed_RequiresBothChordButtons(GamepadButtons buttons, bool expected)
    {
        var state = new GamepadState(buttons);

        Assert.Equal(expected, GamepadMenuChord.IsPressed(state));
    }

    [Fact]
    public void Buttons_IsTheSelectStartChord()
    {
        Assert.Equal(
            GamepadButtons.Select | GamepadButtons.Start,
            GamepadMenuChord.Buttons);
    }
}
