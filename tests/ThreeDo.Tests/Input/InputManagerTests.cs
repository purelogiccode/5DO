using System;
using ThreeDo.Input;

namespace ThreeDo.Tests.InputIo;

public sealed class InputManagerTests
{
    [Fact]
    public void KeyboardMapper_MapsAndReleasesBoundKeys()
    {
        var mapper = new KeyboardInputMapper([PlayerProfile.CreateDefault(1)]);

        Assert.True(mapper.SetKey("z", pressed: true));
        Assert.True(mapper.GetState(0).IsDown(ControllerButton.A));
        Assert.True(mapper.SetKey("Z", pressed: false));
        Assert.False(mapper.GetState(0).IsDown(ControllerButton.A));
        Assert.False(mapper.SetKey("NotAKey", pressed: true));
    }

    [Fact]
    public void KeyboardMapper_AppliesBindingsPerPlayer()
    {
        var profile = PlayerProfile.CreateDefault(1);
        var profile2 = PlayerProfile.CreateDefault(2);
        profile2.Keyboard["Q"] = ControllerButton.C;
        var mapper = new KeyboardInputMapper([profile, profile2]);

        Assert.True(mapper.SetKey("Q", pressed: true));
        Assert.False(mapper.GetState(0).IsDown(ControllerButton.C));
        Assert.True(mapper.GetState(1).IsDown(ControllerButton.C));
    }

    [Fact]
    public void InputManager_MergesKeyboardAndGamepad()
    {
        var profile = InputProfile.CreateDefault();
        var gamepad = new FakeGamepad();
        var manager = new InputManager(profile, gamepad);
        manager.ControllerCount = 2;

        manager.Keyboard.SetKey("Z", pressed: true);
        gamepad.States[1] = new ControllerState(ControllerButton.B);

        var packet = new byte[PbusEncoder.PacketSize];
        manager.GetPbusData(packet);

        Assert.Equal(0x00, packet[2]);
        Assert.Equal(0x81, packet[3]);
        Assert.Equal(0x80, packet[4]);
        Assert.Equal(0x80, packet[5]);
    }

    private sealed class FakeGamepad : IGamepadBackend
    {
        internal ControllerState[] States { get; } = new ControllerState[PbusEncoder.MaxControllers];

        public int ControllerCount => 2;

        public void Poll()
        {
        }

        public ControllerState GetState(int player) => States[player];
    }
}
