using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class PbusChainEncoderTests
{
    [Fact]
    public void EmptyJoypad_EmitsTheOperaJoypadRecordAndPadding()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode([ControllerState.Empty], packet);

        Assert.Equal(4, length);
        Assert.Equal([0x80, 0x00, 0xFF, 0xFF], packet[..length]);
    }

    [Fact]
    public void AllJoypadButtons_UseTheOperaBitOrder()
    {
        ControllerButton all = ControllerButton.A | ControllerButton.B | ControllerButton.C
                               | ControllerButton.P | ControllerButton.X | ControllerButton.L
                               | ControllerButton.R | ControllerButton.Up | ControllerButton.Down
                               | ControllerButton.Left | ControllerButton.Right;
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode([new ControllerState(all)], packet);

        Assert.Equal(4, length);
        Assert.Equal(0x9F, packet[0]);
        Assert.Equal(0xFC, packet[1]);
    }

    [Theory]
    [InlineData(ControllerButton.A, 0x01, 0x00)]
    [InlineData(ControllerButton.Left, 0x02, 0x00)]
    [InlineData(ControllerButton.Right, 0x04, 0x00)]
    [InlineData(ControllerButton.Up, 0x08, 0x00)]
    [InlineData(ControllerButton.Down, 0x10, 0x00)]
    [InlineData(ControllerButton.L, 0x00, 0x04)]
    [InlineData(ControllerButton.R, 0x00, 0x08)]
    [InlineData(ControllerButton.X, 0x00, 0x10)]
    [InlineData(ControllerButton.P, 0x00, 0x20)]
    [InlineData(ControllerButton.C, 0x00, 0x40)]
    [InlineData(ControllerButton.B, 0x00, 0x80)]
    public void SingleJoypadButton_MapsToTheOperaRecord(ControllerButton button, int low, int high)
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode([new ControllerState(button)], packet);

        Assert.Equal(4, length);
        Assert.Equal(low | 0x80, packet[0]);
        Assert.Equal(high, packet[1]);
    }

    [Fact]
    public void SixJoypads_PadToSixteenBytes()
    {
        var controllers = new ControllerState[6];
        for (var i = 0; i < controllers.Length; i++)
        {
            controllers[i] = new ControllerState(ControllerButton.A);
        }

        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode(controllers, packet);

        Assert.Equal(16, length);
        Assert.Equal(0x81, packet[0]);
        Assert.Equal(0x81, packet[2]);
        Assert.Equal(0x81, packet[4]);
        Assert.Equal(0x81, packet[6]);
        Assert.Equal(0x81, packet[8]);
        Assert.Equal(0x81, packet[10]);
        Assert.Equal(0xFF, packet[12]);
    }

    [Fact]
    public void CenteredFlightstick_EmitsNineByteSignatureAndAxes()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode([InputDeviceType.Flightstick], packet);

        Assert.Equal(12, length);
        Assert.Equal([0x01, 0x7B, 0x08, 0x80, 0x20, 0x08, 0x02, 0x00, 0x00], packet[..9]);
        Assert.Equal(0xFF, packet[9]);
    }

    [Fact]
    public void FlightstickAxesClampOutsideTheSignedRange()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new FlightstickState(int.MaxValue, int.MinValue, 0, ControllerButton.None, Fire: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.Flightstick], packet, flightstick: state);

        Assert.Equal(12, length);
        Assert.Equal(0xFF, packet[3]);
        Assert.Equal(0xC0, packet[4]);
        Assert.Equal(0x08, packet[5]);
        Assert.Equal(0x02, packet[6]);
    }

    [Fact]
    public void FlightstickButtons_UseTheOperaBitOrder()
    {
        var state = new FlightstickState(
            0,
            0,
            0,
            ControllerButton.Left | ControllerButton.Right | ControllerButton.Up
                | ControllerButton.Down | ControllerButton.C | ControllerButton.B
                | ControllerButton.A | ControllerButton.P | ControllerButton.X
                | ControllerButton.L | ControllerButton.R,
            Fire: true);
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode([InputDeviceType.Flightstick], packet, flightstick: state);

        Assert.Equal(12, length);
        Assert.Equal(0xFF, packet[7]);
        Assert.Equal(0xF0, packet[8]);
    }

    [Fact]
    public void Mouse_EmitsIdButtonsAndTenBitDeltas()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new MouseState(1, 2, Left: true, Middle: false, Right: false, Shift: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.Mouse], packet, mouse: state);

        Assert.Equal(8, length);
        Assert.Equal([0x49, 0x80, 0x08, 0x01, 0xFF, 0xFF, 0xFF, 0xFF], packet[..length]);
    }

    [Fact]
    public void Mouse_NegativeDeltasUseTwosComplement()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new MouseState(-1, -1, Left: false, Middle: false, Right: false, Shift: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.Mouse], packet, mouse: state);

        Assert.Equal(8, length);
        Assert.Equal(0x49, packet[0]);
        Assert.Equal(0x0F, packet[1]);
        Assert.Equal(0xFF, packet[2]);
        Assert.Equal(0xFF, packet[3]);
    }

    [Fact]
    public void Lightgun_AtOriginEmitsThePortfolioCounter()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode([InputDeviceType.Lightgun], packet);

        Assert.Equal(8, length);
        Assert.Equal([0x4D, 0x00, 0x06, 0x44, 0x7F], packet[..5]);
        Assert.Equal(0xFF, packet[5]);
    }

    [Fact]
    public void Lightgun_AtBottomRightEmitsTheFullCounter()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new LightgunState(319, 239, Trigger: false, Option: false, Reload: false, OffScreen: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.Lightgun], packet, lightgun: state);

        Assert.Equal(8, length);
        Assert.Equal([0x4D, 0x00, 0x9B, 0x0E, 0xFF], packet[..5]);
    }

    [Fact]
    public void Lightgun_ReloadZeroesTheCounterAndPressesTheTrigger()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new LightgunState(100, 100, Trigger: false, Option: false, Reload: true, OffScreen: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.Lightgun], packet, lightgun: state);

        Assert.Equal(8, length);
        Assert.Equal([0x4D, 0x80, 0x00, 0x00, 0x00], packet[..5]);
    }

    [Fact]
    public void Lightgun_OffScreenMapsTriggerToReload()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new LightgunState(100, 100, Trigger: true, Option: false, Reload: false, OffScreen: true);
        var length = PbusChainEncoder.Encode([InputDeviceType.Lightgun], packet, lightgun: state);

        Assert.Equal(8, length);
        Assert.Equal([0x4D, 0x80, 0x00, 0x00, 0x00], packet[..5]);
    }

    [Fact]
    public void ArcadeLightgun_HolsterEmitsAZeroHitCounter()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new ArcadeLightgunState(
            100,
            100,
            Trigger: false,
            Service: false,
            Coins: false,
            Start: false,
            Holster: true,
            OffScreen: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.ArcadeLightgun], packet, arcadeLightgun: state);

        Assert.Equal(8, length);
        Assert.Equal([0x4D, 0x08, 0x06, 0x44, 0x60], packet[..5]);
    }

    [Fact]
    public void ArcadeLightgun_ButtonsUseTheOperaBitOrder()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var state = new ArcadeLightgunState(
            0,
            0,
            Trigger: true,
            Service: true,
            Coins: true,
            Start: true,
            Holster: false,
            OffScreen: false);
        var length = PbusChainEncoder.Encode([InputDeviceType.ArcadeLightgun], packet, arcadeLightgun: state);

        Assert.Equal(8, length);
        Assert.Equal(0xF0, packet[1]);
    }

    [Fact]
    public void OrbatakTrackball_EmitsTheMouseIdAndDeltas()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode(
            [InputDeviceType.OrbatakTrackball],
            packet,
            trackball: new OrbatakTrackballState(1, 2));

        Assert.Equal(8, length);
        Assert.Equal([0x49, 0x00, 0x08, 0x01], packet[..4]);
        Assert.Equal(0xC0, packet[4]);
        Assert.Equal(0x00, packet[5]);
        Assert.Equal(0x00, packet[6]);
    }

    [Fact]
    public void OrbatakButtons_UseTheCabinetBitOrder()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var buttons = new OrbatakButtonsState(
            StartP1: true,
            StartP2: true,
            CoinP1: true,
            CoinP2: true,
            Service: true);
        var length = PbusChainEncoder.Encode(
            [InputDeviceType.OrbatakTrackball],
            packet,
            trackball: OrbatakTrackballState.Empty,
            trackballButtons: buttons);

        Assert.Equal(8, length);
        Assert.Equal(0x37, packet[6]);
    }

    [Fact]
    public void MixedChain_PreservesPortOrderAndPadding()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode(
            [InputDeviceType.Joypad, InputDeviceType.Mouse],
            packet,
            joypad: new ControllerState(ControllerButton.A));

        Assert.Equal(8, length);
        Assert.Equal([0x81, 0x00, 0x49, 0x00, 0x00, 0x00, 0xFF, 0xFF], packet[..length]);
    }

    [Fact]
    public void NonePort_EmitsNoRecord()
    {
        var packet = new byte[PbusChainEncoder.MaxPacketSize];
        var length = PbusChainEncoder.Encode(
            [InputDeviceType.None, InputDeviceType.Joypad],
            packet,
            joypad: new ControllerState(ControllerButton.B));

        Assert.Equal(4, length);
        Assert.Equal(0x80, packet[0]);
        Assert.Equal(0x80, packet[1]);
    }

    [Fact]
    public void DeviceSet_SelectsPerPortDevices()
    {
        var devices = new InputDeviceSet();
        devices.SetDevice(0, InputDeviceType.Lightgun);
        devices.SetDevice(2, InputDeviceType.Mouse);

        Assert.Equal(3, devices.PortCount);
        Assert.Equal(InputDeviceType.Lightgun, devices.GetDevice(0));
        Assert.Equal(InputDeviceType.Joypad, devices.GetDevice(1));
        Assert.Equal(InputDeviceType.Mouse, devices.GetDevice(2));
        Assert.True(devices.HasPeripheral);
    }
}
