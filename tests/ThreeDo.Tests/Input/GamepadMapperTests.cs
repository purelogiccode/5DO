using System;
using ThreeDo.Input;

namespace ThreeDo.Tests.Input;

public sealed class GamepadMapperTests
{
    [Fact]
    public void DefaultProfile_MapsFaceButtonsAndDpad()
    {
        var profile = PlayerProfile.CreateDefault(1);
        var state = new GamepadState(
            GamepadButtons.South | GamepadButtons.West | GamepadButtons.North
            | GamepadButtons.Start | GamepadButtons.DpadUp | GamepadButtons.DpadRight);

        var mapped = GamepadMapper.Map(profile, state);

        Assert.True(mapped.IsDown(ControllerButton.A));
        Assert.True(mapped.IsDown(ControllerButton.B));
        Assert.True(mapped.IsDown(ControllerButton.C));
        Assert.True(mapped.IsDown(ControllerButton.P));
        Assert.True(mapped.IsDown(ControllerButton.Up));
        Assert.True(mapped.IsDown(ControllerButton.Right));
        Assert.False(mapped.IsDown(ControllerButton.Down));
        Assert.False(mapped.IsDown(ControllerButton.Left));
    }

    [Fact]
    public void LeftStick_FallsBackToDpadBindings()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["DpadUp"] = ControllerButton.Up;
        profile.Gamepad["DpadLeft"] = ControllerButton.Left;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.None, LeftStickX: -0.9f, LeftStickY: 0.9f));

        Assert.True(mapped.IsDown(ControllerButton.Up));
        Assert.True(mapped.IsDown(ControllerButton.Left));
        Assert.False(mapped.IsDown(ControllerButton.Down));
        Assert.False(mapped.IsDown(ControllerButton.Right));
    }

    [Fact]
    public void ExplicitStickBindings_DisableTheDpadFallback()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["DpadUp"] = ControllerButton.Up;
        profile.Gamepad["LeftStickUp"] = ControllerButton.C;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.None, LeftStickY: 0.9f));

        Assert.True(mapped.IsDown(ControllerButton.C));
        Assert.False(mapped.IsDown(ControllerButton.Up));
    }

    [Fact]
    public void AnalogTrigger_FallsBackToTheShoulderBinding()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["LeftShoulder"] = ControllerButton.L;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.None, LeftTrigger: 0.8f));

        Assert.True(mapped.IsDown(ControllerButton.L));
    }

    [Fact]
    public void ExplicitTriggerBinding_WinsOverTheShoulderFallback()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["LeftShoulder"] = ControllerButton.L;
        profile.Gamepad["LeftTrigger"] = ControllerButton.C;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.None, LeftTrigger: 0.8f));

        Assert.True(mapped.IsDown(ControllerButton.C));
        Assert.False(mapped.IsDown(ControllerButton.L));
    }

    [Theory]
    [InlineData(0.49f, false)]
    [InlineData(0.5f, false)]
    [InlineData(0.51f, true)]
    public void StickDeadZone_IsExclusive(float axis, bool pressed)
    {
        var profile = new PlayerProfile();
        profile.Gamepad["LeftStickRight"] = ControllerButton.Right;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.None, LeftStickX: axis));

        Assert.Equal(pressed, mapped.IsDown(ControllerButton.Right));
    }

    [Fact]
    public void BindingNames_AreCaseInsensitive()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["south"] = ControllerButton.A;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.South));

        Assert.True(mapped.IsDown(ControllerButton.A));
    }

    [Fact]
    public void RightStickBindings_MapIndependently()
    {
        var profile = new PlayerProfile();
        profile.Gamepad["RightStickUp"] = ControllerButton.C;
        profile.Gamepad["RightStickDown"] = ControllerButton.B;

        var mapped = GamepadMapper.Map(profile, new GamepadState(GamepadButtons.None, RightStickY: -0.8f));

        Assert.True(mapped.IsDown(ControllerButton.B));
        Assert.False(mapped.IsDown(ControllerButton.C));
    }

    [Fact]
    public void InvalidDeadZone_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GamepadMapper.Map(PlayerProfile.CreateDefault(1), GamepadState.Empty, deadZone: 1.5f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GamepadMapper.Map(PlayerProfile.CreateDefault(1), GamepadState.Empty, triggerThreshold: -0.1f));
    }
}
