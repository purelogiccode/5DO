using System;
using FiveDo.Frontend.Avalonia.Input;
using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class DirectInputStateTranslatorTests
{
    [Fact]
    public void FaceShoulderAndMenuButtons_MapToTheVendorNeutralFlags()
    {
        Span<byte> buttons = stackalloc byte[DirectInputStateTranslator.ButtonCount];
        buttons[0] = 1;
        buttons[1] = 1;
        buttons[2] = 1;
        buttons[3] = 1;
        buttons[4] = 1;
        buttons[5] = 1;
        buttons[6] = 1;
        buttons[7] = 1;
        buttons[8] = 1;
        buttons[9] = 1;

        GamepadState state = DirectInputStateTranslator.Translate(
            new int[DirectInputStateTranslator.AxisCount],
            new uint[DirectInputStateTranslator.PovCount],
            buttons,
            0,
            0);

        Assert.True(state.Buttons.HasFlag(GamepadButtons.South));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.East));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.West));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.North));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.LeftShoulder));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.RightShoulder));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.Select));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.Start));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.LeftStick));
        Assert.True(state.Buttons.HasFlag(GamepadButtons.RightStick));
    }

    [Fact]
    public void PovHat_MapsCardinalAndDiagonalAngles()
    {
        AssertPov(0, GamepadButtons.DpadUp);
        AssertPov(4500, GamepadButtons.DpadUp | GamepadButtons.DpadRight);
        AssertPov(9000, GamepadButtons.DpadRight);
        AssertPov(13500, GamepadButtons.DpadDown | GamepadButtons.DpadRight);
        AssertPov(18000, GamepadButtons.DpadDown);
        AssertPov(22500, GamepadButtons.DpadDown | GamepadButtons.DpadLeft);
        AssertPov(27000, GamepadButtons.DpadLeft);
        AssertPov(31500, GamepadButtons.DpadUp | GamepadButtons.DpadLeft);
        AssertPov(0xFFFFFFFF, GamepadButtons.None);
    }

    [Fact]
    public void Axes_NormalizeFromTheDirectInputLogicalRange()
    {
        int[] axes = new int[DirectInputStateTranslator.AxisCount];
        axes[0] = 10000;
        axes[1] = -10000;
        axes[3] = 5000;
        axes[4] = -5000;

        GamepadState state = DirectInputStateTranslator.Translate(
            axes,
            new uint[DirectInputStateTranslator.PovCount],
            new byte[DirectInputStateTranslator.ButtonCount],
            0,
            0);

        Assert.Equal(1f, state.LeftStickX);
        Assert.Equal(-1f, state.LeftStickY);
        Assert.Equal(0.5f, state.RightStickX);
        Assert.Equal(-0.5f, state.RightStickY);
    }

    [Fact]
    public void TriggerValues_SetTheButtonBitsAtTheSharedThreshold()
    {
        GamepadState released = DirectInputStateTranslator.Translate(
            new int[DirectInputStateTranslator.AxisCount],
            new uint[DirectInputStateTranslator.PovCount],
            new byte[DirectInputStateTranslator.ButtonCount],
            0.49f,
            0.49f);
        Assert.False(released.Buttons.HasFlag(GamepadButtons.LeftTrigger));
        Assert.False(released.Buttons.HasFlag(GamepadButtons.RightTrigger));

        GamepadState pressed = DirectInputStateTranslator.Translate(
            new int[DirectInputStateTranslator.AxisCount],
            new uint[DirectInputStateTranslator.PovCount],
            new byte[DirectInputStateTranslator.ButtonCount],
            0.5f,
            1f);
        Assert.True(pressed.Buttons.HasFlag(GamepadButtons.LeftTrigger));
        Assert.True(pressed.Buttons.HasFlag(GamepadButtons.RightTrigger));
        Assert.Equal(0.5f, pressed.LeftTrigger);
        Assert.Equal(1f, pressed.RightTrigger);
    }

    [Fact]
    public void MissingAxes_AreTreatedAsCenteredAndShortButtonArraysAreSafe()
    {
        GamepadState state = DirectInputStateTranslator.Translate(
            ReadOnlySpan<int>.Empty,
            ReadOnlySpan<uint>.Empty,
            new byte[] { 1 },
            0,
            0);

        Assert.Equal(GamepadButtons.South, state.Buttons);
        Assert.Equal(0f, state.LeftStickX);
        Assert.Equal(0f, state.LeftStickY);
    }

    private static void AssertPov(uint value, GamepadButtons expected)
    {
        GamepadButtons buttons = GamepadButtons.None;
        DirectInputStateTranslator.ApplyPov(value, ref buttons);
        Assert.Equal(expected, buttons);
    }
}
