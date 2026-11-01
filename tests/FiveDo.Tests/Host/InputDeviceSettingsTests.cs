using FiveDo.Host;
using FiveDo.Input;

namespace FiveDo.Tests.Host;

public sealed class InputDeviceSettingsTests
{
    [Fact]
    public void Defaults_UseSixJoypads()
    {
        var settings = new AppSettings();

        Assert.Equal(PbusEncoder.MaxControllers, settings.InputDevices.Count);
        for (var i = 0; i < PbusEncoder.MaxControllers; i++)
        {
            Assert.Equal(InputDeviceType.Joypad, settings.GetInputDevice(i));
        }
    }

    [Theory]
    [InlineData("mouse", InputDeviceType.Mouse)]
    [InlineData("Mouse", InputDeviceType.Mouse)]
    [InlineData("lightgun", InputDeviceType.Lightgun)]
    [InlineData("ArcadeLightgun", InputDeviceType.ArcadeLightgun)]
    [InlineData("OrbatakTrackball", InputDeviceType.OrbatakTrackball)]
    [InlineData("None", InputDeviceType.None)]
    public void GetInputDevice_ParsesNamesCaseInsensitively(string name, InputDeviceType expected)
    {
        var settings = new AppSettings();
        settings.InputDevices[0] = name;

        Assert.Equal(expected, settings.GetInputDevice(0));
    }

    [Fact]
    public void GetInputDevice_InvalidNameFallsBackToJoypad()
    {
        var settings = new AppSettings();
        settings.InputDevices[0] = "banana";

        Assert.Equal(InputDeviceType.Joypad, settings.GetInputDevice(0));
    }

    [Fact]
    public void SetInputDevice_GrowsTheList()
    {
        var settings = new AppSettings();
        settings.InputDevices.Clear();

        settings.SetInputDevice(2, InputDeviceType.Mouse);

        Assert.Equal(3, settings.InputDevices.Count);
        Assert.Equal(InputDeviceType.Mouse, settings.GetInputDevice(2));
        Assert.Equal(InputDeviceType.Joypad, settings.GetInputDevice(1));
    }

    [Fact]
    public void ApplyInputDevices_ConfiguresTheManagerAndHighestPort()
    {
        var settings = new AppSettings();
        settings.SetInputDevice(0, InputDeviceType.Joypad);
        settings.SetInputDevice(1, InputDeviceType.Flightstick);
        settings.SetInputDevice(2, InputDeviceType.Lightgun);
        var input = new InputManager(InputProfile.CreateDefault());

        settings.ApplyInputDevices(input);

        Assert.Equal(3, input.ControllerCount);
        Assert.Equal(InputDeviceType.Joypad, input.Devices.GetDevice(0));
        Assert.Equal(InputDeviceType.Flightstick, input.Devices.GetDevice(1));
        Assert.Equal(InputDeviceType.Lightgun, input.Devices.GetDevice(2));
        Assert.True(input.Devices.HasPeripheral);
    }

    [Fact]
    public void ApplyInputDevices_LeavesTheDefaultSinglePadPacket()
    {
        var settings = new AppSettings();
        var input = new InputManager(InputProfile.CreateDefault());
        var packet = new byte[PbusChainEncoder.MaxPacketSize];

        settings.ApplyInputDevices(input);
        var length = input.GetPbusData(packet);

        Assert.Equal(1, input.ControllerCount);
        Assert.Equal(PbusEncoder.PacketSize, length);
        Assert.Equal(0x00, packet[0]);
        Assert.Equal(0x48, packet[1]);
    }
}
