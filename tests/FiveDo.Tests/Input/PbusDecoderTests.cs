using FiveDo.Input;

namespace FiveDo.Tests.Input;

public sealed class PbusDecoderTests
{
    [Fact]
    public void Decode_RoundtripsEveryButtonForEverySlot()
    {
        ControllerButton all = ControllerButton.A | ControllerButton.B | ControllerButton.C
                               | ControllerButton.P | ControllerButton.X | ControllerButton.L
                               | ControllerButton.R | ControllerButton.Up | ControllerButton.Down
                               | ControllerButton.Left | ControllerButton.Right;
        var states = new ControllerState[PbusEncoder.MaxControllers];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = new ControllerState(all);
        }

        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode(states, packet);
        ControllerState[] decoded = PbusDecoder.Decode(packet);

        for (var i = 0; i < decoded.Length; i++)
        {
            Assert.Equal(all, decoded[i].Buttons);
            Assert.True(PbusDecoder.IsConnected(packet, i));
        }
    }

    [Fact]
    public void Decode_EmptyPacketReportsDisconnectedSlots()
    {
        var packet = new byte[PbusEncoder.PacketSize];
        ControllerState[] decoded = PbusDecoder.Decode(packet);

        foreach (ControllerState state in decoded)
        {
            Assert.Equal(ControllerButton.None, state.Buttons);
        }

        Assert.False(PbusDecoder.IsConnected(packet, 0));
        Assert.False(PbusDecoder.IsConnected(packet, 5));
    }

    [Fact]
    public void Decode_ShortPacketReturnsEmptyStates()
    {
        ControllerState[] decoded = PbusDecoder.Decode(new byte[] { 0x00, 0x48 });

        Assert.Equal(ControllerButton.None, decoded[0].Buttons);
        Assert.False(PbusDecoder.IsConnected(new byte[] { 0x00, 0x48 }, 0));
    }

    [Fact]
    public void DecodeController_RejectsInvalidSlots()
    {
        var packet = new byte[PbusEncoder.PacketSize];

        Assert.Throws<System.ArgumentOutOfRangeException>(() => PbusDecoder.DecodeController(packet, -1));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => PbusDecoder.DecodeController(packet, 6));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => PbusDecoder.IsConnected(packet, 6));
    }

    [Fact]
    public void FormatButtons_ReturnsNoneOrTheReferenceOrder()
    {
        Assert.Equal("None", PbusDecoder.FormatButtons(ControllerState.Empty));
        Assert.Equal(
            "A,B,C,P,X,L,R,Up,Down,Left,Right",
            PbusDecoder.FormatButtons(new ControllerState(
                ControllerButton.A | ControllerButton.B | ControllerButton.C
                | ControllerButton.P | ControllerButton.X | ControllerButton.L
                | ControllerButton.R | ControllerButton.Up | ControllerButton.Down
                | ControllerButton.Left | ControllerButton.Right)));
        Assert.Equal(
            "A,Up",
            PbusDecoder.FormatButtons(new ControllerState(ControllerButton.A | ControllerButton.Up)));
    }

    [Fact]
    public void Decode_MatchesEncodedSlotOffsets()
    {
        var packet = new byte[PbusEncoder.PacketSize];
        PbusEncoder.Encode(
            [
                new ControllerState(ControllerButton.A),
                new ControllerState(ControllerButton.B),
                new ControllerState(ControllerButton.C),
                new ControllerState(ControllerButton.L),
                new ControllerState(ControllerButton.R),
                new ControllerState(ControllerButton.Up),
            ],
            packet);

        ControllerState[] decoded = PbusDecoder.Decode(packet);

        Assert.True(decoded[0].IsDown(ControllerButton.A));
        Assert.True(decoded[1].IsDown(ControllerButton.B));
        Assert.True(decoded[2].IsDown(ControllerButton.C));
        Assert.True(decoded[3].IsDown(ControllerButton.L));
        Assert.True(decoded[4].IsDown(ControllerButton.R));
        Assert.True(decoded[5].IsDown(ControllerButton.Up));
    }
}
