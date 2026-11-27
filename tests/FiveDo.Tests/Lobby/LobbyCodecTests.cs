using System;
using System.Net;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Tests.Lobby;

public sealed class LobbyCodecTests
{
    [Fact]
    public void Datagram_RoundTripsPeerEndpoint()
    {
        var expected = new LobbyDatagram(
            LobbyDatagramType.Peer,
            1,
            0x0123456789ABCDEF,
            Guid.Parse("00112233445566778899aabbccddeeff"),
            0xFEDCBA9876543210,
            new IPEndPoint(IPAddress.Parse("203.0.113.9"), 61000),
            41000,
            LobbyTransportMode.Relay);
        var packet = new byte[LobbyDatagramCodec.Size];
        int length = LobbyDatagramCodec.Encode(packet, expected);

        Assert.Equal(LobbyDatagramCodec.Size, length);
        Assert.True(LobbyDatagramCodec.TryDecode(packet, out LobbyDatagram actual));
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Slot, actual.Slot);
        Assert.Equal(expected.RoomId, actual.RoomId);
        Assert.Equal(expected.Token, actual.Token);
        Assert.Equal(expected.Nonce, actual.Nonce);
        Assert.Equal(expected.PeerEndPoint, actual.PeerEndPoint);
        Assert.Equal(expected.RelayPort, actual.RelayPort);
        Assert.Equal(expected.Mode, actual.Mode);
    }

    [Fact]
    public void Datagram_RejectsBadMagicAndLength()
    {
        var packet = new byte[LobbyDatagramCodec.Size];
        _ = LobbyDatagramCodec.Encode(
            packet,
            new LobbyDatagram(
                LobbyDatagramType.Register,
                0,
                1,
                Guid.Empty,
                0,
                null,
                0,
                LobbyTransportMode.Auto));

        Assert.True(LobbyDatagramCodec.TryDecode(packet, out _));
        packet[0] = (byte)'X';
        Assert.False(LobbyDatagramCodec.TryDecode(packet, out _));
        Assert.False(LobbyDatagramCodec.TryDecode(packet.AsSpan(0, packet.Length - 1), out _));
    }

    [Fact]
    public void RelayConnect_RoundTrips()
    {
        var packet = new byte[LobbyRelayProtocol.ConnectSize];
        Guid token = Guid.Parse("00112233445566778899aabbccddeeff");
        int length = LobbyRelayProtocol.EncodeConnect(packet, 42, token, 1);

        Assert.Equal(LobbyRelayProtocol.ConnectSize, length);
        Assert.True(
            LobbyRelayProtocol.TryDecodeConnect(packet, out ulong roomId, out Guid actualToken, out byte slot));
        Assert.Equal(42UL, roomId);
        Assert.Equal(token, actualToken);
        Assert.Equal(1, slot);
    }

    [Fact]
    public void PairedPreamble_IsRecognized()
    {
        Assert.True(LobbyRelayProtocol.IsPairedPreamble(LobbyRelayProtocol.PairedPreamble.Span));
        Assert.False(LobbyRelayProtocol.IsPairedPreamble(new byte[LobbyRelayProtocol.PairedSize]));
    }
}
