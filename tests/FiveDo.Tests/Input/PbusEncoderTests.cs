using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class PbusEncoderTests
{
    [Fact]
    public void EmptyPacket_HasReferenceHeaderAndConnectivityBits()
    {
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode([ControllerState.Empty], packet);

        Assert.Equal(0x00, packet[0]);
        Assert.Equal(0x48, packet[1]);
        Assert.Equal(0x00, packet[2]);
        Assert.Equal(0x80, packet[3]);
        Assert.Equal(0x80, packet[13]);
        Assert.Equal(0x00, packet[15]);
    }

    [Fact]
    public void AllButtons_MapToTheReferenceLowAndHighBytes()
    {
        var all = ControllerButton.A | ControllerButton.B | ControllerButton.C
            | ControllerButton.P | ControllerButton.X | ControllerButton.L
            | ControllerButton.R | ControllerButton.Up | ControllerButton.Down
            | ControllerButton.Left | ControllerButton.Right;
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode([new ControllerState(all)], packet);

        Assert.Equal(0xFC, packet[2]);
        Assert.Equal(0x9F, packet[3]);
    }

    [Theory]
    [InlineData(ControllerButton.L, 0x04, 0x00)]
    [InlineData(ControllerButton.R, 0x08, 0x00)]
    [InlineData(ControllerButton.X, 0x10, 0x00)]
    [InlineData(ControllerButton.P, 0x20, 0x00)]
    [InlineData(ControllerButton.C, 0x40, 0x00)]
    [InlineData(ControllerButton.B, 0x80, 0x00)]
    [InlineData(ControllerButton.A, 0x00, 0x01)]
    [InlineData(ControllerButton.Left, 0x00, 0x02)]
    [InlineData(ControllerButton.Right, 0x00, 0x04)]
    [InlineData(ControllerButton.Up, 0x00, 0x08)]
    [InlineData(ControllerButton.Down, 0x00, 0x10)]
    public void SingleButton_MapsToTheDocumentedBit(ControllerButton button, int low, int high)
    {
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode([new ControllerState(button)], packet);

        Assert.Equal(low, packet[2]);
        Assert.Equal(high | 0x80, packet[3]);
    }

    [Fact]
    public void SixControllers_UseWiredDeviceOrder()
    {
        var states = new ControllerState[6];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new ControllerState(ControllerButton.A);
        }

        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode(states, packet);

        Assert.Equal(0x81, packet[3]);
        Assert.Equal(0x81, packet[5]);
        Assert.Equal(0x81, packet[7]);
        Assert.Equal(0x81, packet[9]);
        Assert.Equal(0x81, packet[11]);
        Assert.Equal(0x81, packet[15]);
        Assert.Equal(0x80, packet[13]);
    }

    [Fact]
    public void PbusSource_TracksConnectedCount()
    {
        var source = new PbusSource();
        Assert.Equal(1, source.ControllerCount);

        source[2] = new ControllerState(ControllerButton.B);
        Assert.Equal(3, source.ControllerCount);

        var packet = new byte[PbusEncoder.PacketSize];
        Assert.Equal(PbusEncoder.PacketSize, source.GetPbusData(packet));
        Assert.Equal(0x80, packet[6]);
        Assert.Equal(0x80, packet[7]);
        Assert.Equal(0x00, packet[9]);
    }
}
