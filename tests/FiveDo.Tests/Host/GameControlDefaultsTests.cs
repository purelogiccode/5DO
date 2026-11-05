using System.Collections.Generic;
using FiveDo.Host;
using FiveDo.Input;

namespace FiveDo.Tests.Host;

public sealed class GameControlDefaultsTests
{
    [Fact]
    public void ParseDeviceSpec_FillsEightPorts()
    {
        IReadOnlyList<string>? devices = GameControlDefaults.ParseDeviceSpec(
            "1=lightgun,2=arcade-lightgun,8=orbatak-trackball");

        Assert.NotNull(devices);
        Assert.Equal(PbusEncoder.MaxPorts, devices.Count);
        Assert.Equal(InputDeviceType.Lightgun.ToString(), devices[0]);
        Assert.Equal(InputDeviceType.ArcadeLightgun.ToString(), devices[1]);
        Assert.Equal(InputDeviceType.Joypad.ToString(), devices[2]);
        Assert.Equal(InputDeviceType.OrbatakTrackball.ToString(), devices[7]);
    }

    [Fact]
    public void ParseDeviceSpec_RejectsEmptyAndInvalidSpecs()
    {
        Assert.Null(GameControlDefaults.ParseDeviceSpec(string.Empty));
        Assert.Null(GameControlDefaults.ParseDeviceSpec("1=banana"));
        Assert.Null(GameControlDefaults.ParseDeviceSpec("0=lightgun"));
        Assert.Null(GameControlDefaults.ParseDeviceSpec("9=lightgun"));
    }

    [Fact]
    public void TryGetInputDevices_PrefersTheDatabaseSpec()
    {
        IReadOnlyList<string>? devices = GameControlDefaults.TryGetInputDevices(
            "Crime Patrol (USA)",
            "1=arcade-lightgun,2=none");

        Assert.NotNull(devices);
        Assert.Equal(InputDeviceType.ArcadeLightgun.ToString(), devices[0]);
        Assert.Equal(InputDeviceType.None.ToString(), devices[1]);
    }

    [Fact]
    public void TryGetInputDevices_FallsBackToTheTitlePrefix()
    {
        IReadOnlyList<string>? devices = GameControlDefaults.TryGetInputDevices("Orbatak (USA)");

        Assert.NotNull(devices);
        Assert.Equal(InputDeviceType.OrbatakTrackball.ToString(), devices[0]);
    }
}
