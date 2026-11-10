using System.Collections.Generic;
using FiveDo.Frontend.Avalonia.Input;
using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class MacOsGamepadMapperTests
{
    [Fact]
    public void ButtonUsages_MapToNeutralGamepadFlags()
    {
        var elements = new List<MacOsHidElementSample>();
        for (ushort usage = 1; usage <= 14; usage++)
        {
            elements.Add(Button(usage, pressed: true));
        }

        GamepadState state = MacOsHidGamepadMapper.Map(elements.ToArray());

        Assert.True(Has(state.Buttons, GamepadButtons.South));
        Assert.True(Has(state.Buttons, GamepadButtons.East));
        Assert.True(Has(state.Buttons, GamepadButtons.West));
        Assert.True(Has(state.Buttons, GamepadButtons.North));
        Assert.True(Has(state.Buttons, GamepadButtons.LeftShoulder));
        Assert.True(Has(state.Buttons, GamepadButtons.RightShoulder));
        Assert.True(Has(state.Buttons, GamepadButtons.Select));
        Assert.True(Has(state.Buttons, GamepadButtons.Start));
        Assert.True(Has(state.Buttons, GamepadButtons.LeftStick));
        Assert.True(Has(state.Buttons, GamepadButtons.RightStick));
        Assert.True(Has(state.Buttons, GamepadButtons.DpadUp));
        Assert.True(Has(state.Buttons, GamepadButtons.DpadDown));
        Assert.True(Has(state.Buttons, GamepadButtons.DpadLeft));
        Assert.True(Has(state.Buttons, GamepadButtons.DpadRight));
    }

    [Fact]
    public void ReleasedButtons_AndUnknownUsages_AreIgnored()
    {
        MacOsHidElementSample[] elements =
        [
            Button(1, pressed: false),
            Button(99, pressed: true),
            new MacOsHidElementSample(0x0C, 0xE9, 0, 1, 1),
        ];

        GamepadState state = MacOsHidGamepadMapper.Map(elements);

        Assert.Equal(GamepadButtons.None, state.Buttons);
    }

    [Fact]
    public void Sticks_NormalizeAndInvertY()
    {
        MacOsHidElementSample[] elements =
        [
            Axis(MacOsHidGamepadMapper.X, 0, -1, 1),
            Axis(MacOsHidGamepadMapper.Y, 1, -1, 1),
            Axis(MacOsHidGamepadMapper.Rx, -1, -1, 1),
            Axis(MacOsHidGamepadMapper.Ry, -1, -1, 1),
        ];

        GamepadState state = MacOsHidGamepadMapper.Map(elements);

        Assert.Equal(0f, state.LeftStickX, 3);
        Assert.Equal(-1f, state.LeftStickY, 3);
        Assert.Equal(-1f, state.RightStickX, 3);
        Assert.Equal(1f, state.RightStickY, 3);
    }

    [Fact]
    public void UnsignedAxisRange_NormalizesFromLogicalMinimum()
    {
        MacOsHidElementSample[] elements =
        [
            Axis(MacOsHidGamepadMapper.X, 128, 0, 255),
            Axis(MacOsHidGamepadMapper.Y, 64, 0, 255),
        ];

        GamepadState state = MacOsHidGamepadMapper.Map(elements);

        Assert.Equal(0.0039f, state.LeftStickX, 3);
        Assert.Equal(0.498f, state.LeftStickY, 3);
    }

    [Fact]
    public void Triggers_ReportAnalogValueAndThresholdFlag()
    {
        MacOsHidElementSample[] pressed =
        [
            Axis(MacOsHidGamepadMapper.Z, 192, 0, 255),
            Axis(MacOsHidGamepadMapper.Rz, 128, 0, 255),
        ];

        GamepadState state = MacOsHidGamepadMapper.Map(pressed);

        Assert.Equal(0.7529f, state.LeftTrigger, 3);
        Assert.Equal(0.502f, state.RightTrigger, 3);
        Assert.True(Has(state.Buttons, GamepadButtons.LeftTrigger));
        Assert.True(Has(state.Buttons, GamepadButtons.RightTrigger));

        GamepadState released = MacOsHidGamepadMapper.Map(
        [
            Axis(MacOsHidGamepadMapper.Z, 64, 0, 255),
            Axis(MacOsHidGamepadMapper.Rz, 0, 0, 255),
        ]);

        Assert.Equal(0.251f, released.LeftTrigger, 3);
        Assert.Equal(0f, released.RightTrigger, 3);
        Assert.False(Has(released.Buttons, GamepadButtons.LeftTrigger));
        Assert.False(Has(released.Buttons, GamepadButtons.RightTrigger));
    }

    [Fact]
    public void ZRzOnly_FallsBackToTheRightStick()
    {
        MacOsHidElementSample[] elements =
        [
            Axis(MacOsHidGamepadMapper.Z, 1, -1, 1),
            Axis(MacOsHidGamepadMapper.Rz, 1, -1, 1),
        ];

        GamepadState state = MacOsHidGamepadMapper.Map(elements);

        Assert.Equal(1f, state.RightStickX, 3);
        Assert.Equal(-1f, state.RightStickY, 3);
        Assert.Equal(0f, state.LeftTrigger, 3);
        Assert.Equal(0f, state.RightTrigger, 3);
    }

    [Theory]
    [InlineData(0, GamepadButtons.DpadUp)]
    [InlineData(1, GamepadButtons.DpadUp | GamepadButtons.DpadRight)]
    [InlineData(2, GamepadButtons.DpadRight)]
    [InlineData(3, GamepadButtons.DpadDown | GamepadButtons.DpadRight)]
    [InlineData(4, GamepadButtons.DpadDown)]
    [InlineData(5, GamepadButtons.DpadDown | GamepadButtons.DpadLeft)]
    [InlineData(6, GamepadButtons.DpadLeft)]
    [InlineData(7, GamepadButtons.DpadUp | GamepadButtons.DpadLeft)]
    [InlineData(8, GamepadButtons.None)]
    [InlineData(15, GamepadButtons.None)]
    public void EightDirectionHat_MapsToDpad(int value, GamepadButtons expected)
    {
        GamepadState state = MacOsHidGamepadMapper.Map(
        [
            new MacOsHidElementSample(
                MacOsHidGamepadMapper.GenericDesktopPage,
                MacOsHidGamepadMapper.HatSwitch,
                0,
                7,
                value),
        ]);

        Assert.Equal(expected, state.Buttons);
    }

    [Theory]
    [InlineData(0, GamepadButtons.DpadUp)]
    [InlineData(1, GamepadButtons.DpadRight)]
    [InlineData(2, GamepadButtons.DpadDown)]
    [InlineData(3, GamepadButtons.DpadLeft)]
    public void FourDirectionHat_MapsToCardinalDpad(int value, GamepadButtons expected)
    {
        GamepadState state = MacOsHidGamepadMapper.Map(
        [
            new MacOsHidElementSample(
                MacOsHidGamepadMapper.GenericDesktopPage,
                MacOsHidGamepadMapper.HatSwitch,
                0,
                3,
                value),
        ]);

        Assert.Equal(expected, state.Buttons);
    }

    [Fact]
    public void MacOsState_MapsThroughTheDefaultEightPlayerProfile()
    {
        GamepadState state = MacOsHidGamepadMapper.Map(
        [
            Button(1, pressed: true),
            Button(3, pressed: true),
            Button(4, pressed: true),
            Button(8, pressed: true),
        ]);

        ControllerState mapped = GamepadMapper.Map(PlayerProfile.CreateDefault(1), state);

        Assert.True(mapped.IsDown(ControllerButton.A));
        Assert.True(mapped.IsDown(ControllerButton.B));
        Assert.True(mapped.IsDown(ControllerButton.C));
        Assert.True(mapped.IsDown(ControllerButton.P));
    }

    private static MacOsHidElementSample Button(ushort usage, bool pressed)
    {
        return new MacOsHidElementSample(
            MacOsHidGamepadMapper.ButtonPage,
            usage,
            0,
            1,
            pressed ? 1 : 0);
    }

    private static MacOsHidElementSample Axis(ushort usage, int value, int minimum, int maximum)
    {
        return new MacOsHidElementSample(
            MacOsHidGamepadMapper.GenericDesktopPage,
            usage,
            minimum,
            maximum,
            value);
    }

    private static bool Has(GamepadButtons value, GamepadButtons flag)
    {
        return (value & flag) != GamepadButtons.None;
    }
}
