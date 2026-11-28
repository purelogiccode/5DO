using System;
using FiveDo.Host.Netplay.Lobby;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Tests.Netplay;

/// <summary>Covers the lobby room descriptor used by the desktop netplay dialog.</summary>
public sealed class LobbyNetplaySessionTests
{
    [Fact]
    public void ForHost_ParsesTheRoomAndHostToken()
    {
        Guid token = Guid.NewGuid();
        RoomConnectionInfo room = CreateRoom("ABC123", "0123456789abcdef");
        var response = new CreateRoomResponse
        {
            Room = room,
            HostToken = token.ToString("N"),
        };

        LobbyNetplaySession session = LobbyNetplaySession.ForHost(
            new Uri("https://lobby.example.com"),
            response,
            LobbyTransportMode.Auto,
            TimeSpan.FromSeconds(3));

        Assert.Equal("ABC123", session.JoinCode);
        Assert.Equal(0x0123456789abcdefUL, session.RoomId);
        Assert.Equal(token, session.Token);
        Assert.Equal((byte)0, session.Slot);
        Assert.True(session.IsHost);
        Assert.Equal(token.ToString("N"), session.HostToken);
        Assert.Same(room, session.Room);
    }

    [Fact]
    public void ForJoin_ParsesTheSlotTokenAndTransportOptions()
    {
        Guid token = Guid.NewGuid();
        RoomConnectionInfo room = CreateRoom("XYZ789", "fedcba9876543210");
        var response = new JoinRoomResponse
        {
            Room = room,
            SlotToken = token.ToString("N"),
            Slot = 1,
        };

        LobbyNetplaySession session = LobbyNetplaySession.ForJoin(
            new Uri("https://lobby.example.com"),
            response,
            LobbyTransportMode.Relay,
            TimeSpan.FromSeconds(5));

        Assert.Equal("XYZ789", session.JoinCode);
        Assert.Equal(0xfedcba9876543210UL, session.RoomId);
        Assert.Equal(token, session.Token);
        Assert.Equal((byte)1, session.Slot);
        Assert.False(session.IsHost);
        Assert.Null(session.HostToken);
        Assert.Equal(LobbyTransportMode.Relay, session.Mode);
        Assert.Equal(TimeSpan.FromSeconds(5), session.DirectTimeout);
    }

    [Fact]
    public void ForHost_RejectsMalformedTokens()
    {
        var response = new CreateRoomResponse
        {
            Room = CreateRoom("ABC123", "0123456789abcdef"),
            HostToken = "not-a-hex-token",
        };

        Assert.Throws<FormatException>(
            () => LobbyNetplaySession.ForHost(
                new Uri("https://lobby.example.com"),
                response,
                LobbyTransportMode.Auto,
                TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void ForHost_RejectsMalformedRoomIds()
    {
        var response = new CreateRoomResponse
        {
            Room = CreateRoom("ABC123", "10000000000000000"),
            HostToken = Guid.NewGuid().ToString("N"),
        };

        Assert.Throws<FormatException>(
            () => LobbyNetplaySession.ForHost(
                new Uri("https://lobby.example.com"),
                response,
                LobbyTransportMode.Auto,
                TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void ForJoin_RejectsAnOutOfRangeSlot()
    {
        JoinRoomResponse CreateResponse(int slot) => new()
        {
            Room = CreateRoom("XYZ789", "fedcba9876543210"),
            SlotToken = Guid.NewGuid().ToString("N"),
            Slot = slot,
        };

        Assert.Throws<FormatException>(
            () => LobbyNetplaySession.ForJoin(
                new Uri("https://lobby.example.com"),
                CreateResponse(0),
                LobbyTransportMode.Auto,
                TimeSpan.FromSeconds(3)));
        Assert.Throws<FormatException>(
            () => LobbyNetplaySession.ForJoin(
                new Uri("https://lobby.example.com"),
                CreateResponse(300),
                LobbyTransportMode.Auto,
                TimeSpan.FromSeconds(3)));
    }

    private static RoomConnectionInfo CreateRoom(string code, string roomId)
    {
        return new RoomConnectionInfo
        {
            UdpRoomId = roomId,
            JoinCode = code,
            Emulator = LobbyProtocol.Emulator5Do,
            ProtocolVersion = LobbyProtocol.Version,
            ContentId = "disc",
            MaxPlayers = 2,
            Signal = new LobbyEndpoint("lobby.example.com", 443),
            SignalPath = LobbyProtocol.SignalPath,
            ControlRelay = new LobbyEndpoint("relay.example.com", 40000),
            UdpRendezvous = new LobbyEndpoint("relay.example.com", 5700),
            UdpRelay = new LobbyEndpoint("relay.example.com", 41000),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
        };
    }
}
