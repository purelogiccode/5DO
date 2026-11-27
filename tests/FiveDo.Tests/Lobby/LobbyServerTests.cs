using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Host.Netplay.Lobby;
using FiveDo.Lobby;
using FiveDo.Lobby.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FiveDo.Tests.Lobby;

public sealed class LobbyServerTests
{
    [Fact]
    public void RoomManager_CreatesJoinsAndAuthenticates()
    {
        RoomManager manager = CreateManager(CreateOptions());
        Room room = manager.CreateRoom(CreateRequest(), DateTimeOffset.UtcNow);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RoomJoin join = manager.Join(room.JoinCode, CreateJoinRequest(), now);

        Assert.Equal(1, join.Slot);
        Assert.True(room.TryAuthenticate(room.HostToken, out int hostSlot));
        Assert.True(room.TryAuthenticate(join.SlotToken, out int joinSlot));
        Assert.Equal(0, hostSlot);
        Assert.Equal(1, joinSlot);

        RoomStatus status = room.Snapshot(now);
        Assert.Equal(2, status.Players.Length);
        Assert.True(status.Players[0].Connected);
        Assert.True(status.Players[1].Connected);
        Assert.Equal("host", status.Players[0].DisplayName);
    }

    [Fact]
    public void RoomManager_RejectsMismatchedContent()
    {
        RoomManager manager = CreateManager(CreateOptions());
        Room room = manager.CreateRoom(CreateRequest(), DateTimeOffset.UtcNow);
        JoinRoomRequest request = CreateJoinRequest() with { ContentId = "other-disc" };

        LobbyApiException exception = Assert.Throws<LobbyApiException>(
            () => manager.Join(room.JoinCode, request, DateTimeOffset.UtcNow));
        Assert.Equal("content_mismatch", exception.Code);
    }

    [Fact]
    public void RoomManager_EnforcesPerIpRoomQuota()
    {
        LobbyOptions options = CreateOptions();
        options.MaxRoomsPerIp = 2;
        RoomManager manager = CreateManager(options);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        _ = manager.CreateRoom(CreateRequest(), now, "203.0.113.10");
        _ = manager.CreateRoom(CreateRequest(), now, "203.0.113.10");
        LobbyApiException exception = Assert.Throws<LobbyApiException>(
            () => manager.CreateRoom(CreateRequest(), now, "203.0.113.10"));
        Assert.Equal("room_quota", exception.Code);

        Room other = manager.CreateRoom(CreateRequest(), now, "203.0.113.11");
        Assert.Equal("203.0.113.11", other.CreatedByIp);
    }

    [Fact]
    public void RoomManager_RemovesExpiredRooms()
    {
        LobbyOptions options = CreateOptions();
        options.RoomLifetimeMinutes = 1;
        options.RoomIdleTimeoutMinutes = 1;
        RoomManager manager = CreateManager(options);
        DateTimeOffset created = DateTimeOffset.UtcNow;
        _ = manager.CreateRoom(CreateRequest(), created);

        Assert.Equal(0, manager.RemoveExpired(created.AddSeconds(30)));
        Assert.Equal(1, manager.RemoveExpired(created.AddMinutes(2)));
        Assert.Equal(0, manager.Count);
    }

    [Fact]
    public async Task TcpRelay_PairsAndRelaysBytes()
    {
        LobbyOptions options = CreateOptions(relay: true);
        options.ControlRelayPortStart = 0;
        options.ControlRelayPortEnd = 0;
        options.UdpRelayPortStart = 0;
        options.UdpRelayPortEnd = 0;
        RoomManager manager = CreateManager(options);
        Room room = manager.CreateRoom(CreateRequest(), DateTimeOffset.UtcNow);
        RoomJoin join = manager.Join(room.JoinCode, CreateJoinRequest(), DateTimeOffset.UtcNow);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Task<TcpClient> hostTask = ConnectRelayAsync(
            room.ControlRelayPort,
            room.UdpRoomId,
            room.HostToken,
            0,
            cancellation.Token);
        Task<TcpClient> joinTask = ConnectRelayAsync(
            room.ControlRelayPort,
            room.UdpRoomId,
            join.SlotToken,
            (byte)join.Slot,
            cancellation.Token);
        TcpClient[] clients = await Task.WhenAll(hostTask, joinTask).ConfigureAwait(true);
        using TcpClient host = clients[0];
        using TcpClient joined = clients[1];
        NetworkStream hostStream = host.GetStream();
        NetworkStream joinStream = joined.GetStream();

        byte[] hostBytes = "host-to-join"u8.ToArray();
        await hostStream.WriteAsync(hostBytes, cancellation.Token).ConfigureAwait(true);
        var received = new byte[hostBytes.Length];
        await joinStream.ReadExactlyAsync(received, cancellation.Token).ConfigureAwait(true);
        Assert.Equal(hostBytes, received);

        byte[] joinBytes = "join-to-host"u8.ToArray();
        await joinStream.WriteAsync(joinBytes, cancellation.Token).ConfigureAwait(true);
        var response = new byte[joinBytes.Length];
        await hostStream.ReadExactlyAsync(response, cancellation.Token).ConfigureAwait(true);
        Assert.Equal(joinBytes, response);
    }

    [Fact]
    public async Task UdpRendezvousAndRelay_ForwardOpaqueDatagrams()
    {
        LobbyOptions options = CreateOptions(relay: true);
        options.UdpPort = 0;
        options.ControlRelayPortStart = 0;
        options.ControlRelayPortEnd = 0;
        options.UdpRelayPortStart = 0;
        options.UdpRelayPortEnd = 0;
        RoomManager manager = CreateManager(options);
        using var udpService = new UdpLobbyService(
            manager,
            Options.Create(options),
            NullLogger<UdpLobbyService>.Instance);
        await udpService.StartAsync(CancellationToken.None).ConfigureAwait(true);
        try
        {
            Room room = manager.CreateRoom(CreateRequest(), DateTimeOffset.UtcNow);
            RoomJoin join = manager.Join(room.JoinCode, CreateJoinRequest(), DateTimeOffset.UtcNow);
            var rendezvous = new IPEndPoint(IPAddress.Loopback, udpService.Port);
            using var host = CreateUdpSocket();
            using var joined = CreateUdpSocket();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            Send(
                host,
                new LobbyDatagram(
                    LobbyDatagramType.Register,
                    0,
                    room.UdpRoomId,
                    Guid.ParseExact(room.HostToken, "N"),
                    0,
                    null,
                    room.UdpRelayPort,
                    LobbyTransportMode.Auto),
                rendezvous);
            _ = await ReceiveUntilAsync(host, LobbyDatagramType.RegisterAck, cancellation.Token)
                .ConfigureAwait(true);

            Send(
                joined,
                new LobbyDatagram(
                    LobbyDatagramType.Register,
                    1,
                    room.UdpRoomId,
                    Guid.ParseExact(join.SlotToken, "N"),
                    0,
                    null,
                    room.UdpRelayPort,
                    LobbyTransportMode.Auto),
                rendezvous);
            _ = await ReceiveUntilAsync(joined, LobbyDatagramType.RegisterAck, cancellation.Token)
                .ConfigureAwait(true);
            LobbyDatagram peer = await ReceiveUntilAsync(host, LobbyDatagramType.Peer, cancellation.Token)
                .ConfigureAwait(true);
            Assert.NotNull(peer.PeerEndPoint);

            var relay = new IPEndPoint(IPAddress.Loopback, room.UdpRelayPort);
            Send(
                host,
                new LobbyDatagram(
                    LobbyDatagramType.RelayHello,
                    0,
                    room.UdpRoomId,
                    Guid.ParseExact(room.HostToken, "N"),
                    0,
                    null,
                    room.UdpRelayPort,
                    LobbyTransportMode.Relay),
                relay);
            _ = await ReceiveUntilAsync(host, LobbyDatagramType.RelayAck, cancellation.Token)
                .ConfigureAwait(true);
            Send(
                joined,
                new LobbyDatagram(
                    LobbyDatagramType.RelayHello,
                    1,
                    room.UdpRoomId,
                    Guid.ParseExact(join.SlotToken, "N"),
                    0,
                    null,
                    room.UdpRelayPort,
                    LobbyTransportMode.Relay),
                relay);
            _ = await ReceiveUntilAsync(joined, LobbyDatagramType.RelayAck, cancellation.Token)
                .ConfigureAwait(true);

            byte[] forward = [1, 2, 3, 4];
            SendRaw(host, forward, relay);
            byte[] received = await ReceiveRawAsync(joined, cancellation.Token).ConfigureAwait(true);
            Assert.Equal(forward, received);

            byte[] reverse = [5, 6, 7, 8];
            SendRaw(joined, reverse, relay);
            byte[] reverseReceived = await ReceiveRawAsync(host, cancellation.Token).ConfigureAwait(true);
            Assert.Equal(reverse, reverseReceived);
        }
        finally
        {
            await udpService.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task LobbyTransport_SendsDirectAndRelayedPayloads()
    {
        LobbyOptions options = CreateOptions(relay: true);
        options.UdpPort = 0;
        options.ControlRelayPortStart = 0;
        options.ControlRelayPortEnd = 0;
        options.UdpRelayPortStart = 0;
        options.UdpRelayPortEnd = 0;
        RoomManager manager = CreateManager(options);
        using var udpService = new UdpLobbyService(
            manager,
            Options.Create(options),
            NullLogger<UdpLobbyService>.Instance);
        await udpService.StartAsync(CancellationToken.None).ConfigureAwait(true);
        try
        {
            Room room = manager.CreateRoom(CreateRequest(), DateTimeOffset.UtcNow);
            RoomJoin join = manager.Join(room.JoinCode, CreateJoinRequest(), DateTimeOffset.UtcNow);
            RoomConnectionInfo roomInfo = room.ToConnectionInfo("127.0.0.1", 0, udpService.Port);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var host = CreateTransport(
                roomInfo,
                room.HostToken,
                0,
                LobbyTransportMode.Direct,
                udpService.Port);
            using var joined = CreateTransport(
                roomInfo,
                join.SlotToken,
                (byte)join.Slot,
                LobbyTransportMode.Direct,
                udpService.Port);
            var joinBuffer = new byte[256];

            await WaitForRemoteAsync(host, joined, joinBuffer, cancellation.Token).ConfigureAwait(true);
            Assert.Equal(LobbyTransportMode.Direct, host.TransportMode);
            Assert.Equal(LobbyTransportMode.Direct, joined.TransportMode);

            await SendUntilReceivedAsync(
                host,
                joined,
                [9, 8, 7],
                joinBuffer,
                cancellation.Token).ConfigureAwait(true);
        }
        finally
        {
            await udpService.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private static LobbyOptions CreateOptions(bool relay = false)
    {
        return new LobbyOptions
        {
            EnableRelay = relay,
            MaxRooms = 16,
            RoomLifetimeMinutes = 30,
            RoomIdleTimeoutMinutes = 10,
            UdpPort = 0,
        };
    }

    private static RoomManager CreateManager(LobbyOptions options)
    {
        return new RoomManager(Options.Create(options), NullLoggerFactory.Instance);
    }

    private static CreateRoomRequest CreateRequest()
    {
        return new CreateRoomRequest
        {
            Emulator = LobbyProtocol.Emulator5Do,
            ProtocolVersion = 1,
            ContentId = "test-disc",
            ContentHash = "deadbeef",
            DisplayName = "host",
            MaxPlayers = 2,
        };
    }

    private static JoinRoomRequest CreateJoinRequest()
    {
        return new JoinRoomRequest
        {
            Emulator = LobbyProtocol.Emulator5Do,
            ProtocolVersion = 1,
            ContentId = "test-disc",
            ContentHash = "deadbeef",
            DisplayName = "join",
        };
    }

    private static LobbyNetplayTransport CreateTransport(
        RoomConnectionInfo room,
        string token,
        byte slot,
        LobbyTransportMode mode,
        int rendezvousPort)
    {
        return new LobbyNetplayTransport(
            new LobbyTransportOptions
            {
                Host = "127.0.0.1",
                RendezvousPort = rendezvousPort,
                RelayPort = room.UdpRelay.Port,
                RoomId = ulong.Parse(
                    room.UdpRoomId,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture),
                Token = Guid.ParseExact(token, "N"),
                Slot = slot,
                Mode = mode,
                DirectTimeout = TimeSpan.FromSeconds(2),
            });
    }

    private static Socket CreateUdpSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }

    private static void Send(Socket socket, LobbyDatagram datagram, IPEndPoint endpoint)
    {
        var packet = new byte[LobbyDatagramCodec.Size];
        int length = LobbyDatagramCodec.Encode(packet, datagram);
        _ = socket.SendTo(packet.AsSpan(0, length), endpoint);
    }

    private static void SendRaw(Socket socket, byte[] payload, IPEndPoint endpoint)
    {
        _ = socket.SendTo(payload, endpoint);
    }

    private static async Task<LobbyDatagram> ReceiveUntilAsync(
        Socket socket,
        LobbyDatagramType expected,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[LobbyProtocol.MaxDatagramSize];
        EndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            SocketReceiveFromResult result = await socket
                .ReceiveFromAsync(buffer, SocketFlags.None, any, cancellationToken)
                .ConfigureAwait(true);
            if (LobbyDatagramCodec.TryDecode(buffer.AsSpan(0, result.ReceivedBytes), out LobbyDatagram datagram)
                && datagram.Type == expected)
            {
                return datagram;
            }
        }
    }

    private static async Task<byte[]> ReceiveRawAsync(Socket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[LobbyProtocol.MaxDatagramSize];
        EndPoint any = new IPEndPoint(IPAddress.Any, 0);
        SocketReceiveFromResult result = await socket
            .ReceiveFromAsync(buffer, SocketFlags.None, any, cancellationToken)
            .ConfigureAwait(true);
        return buffer.AsSpan(0, result.ReceivedBytes).ToArray();
    }

    private static async Task<TcpClient> ConnectRelayAsync(
        int port,
        ulong roomId,
        string token,
        byte slot,
        CancellationToken cancellationToken)
    {
        var client = new TcpClient
        {
            NoDelay = true,
        };
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(true);
            NetworkStream stream = client.GetStream();
            var connect = new byte[LobbyRelayProtocol.ConnectSize];
            _ = LobbyRelayProtocol.EncodeConnect(connect, roomId, Guid.ParseExact(token, "N"), slot);
            await stream.WriteAsync(connect, cancellationToken).ConfigureAwait(true);
            var paired = new byte[LobbyRelayProtocol.PairedSize];
            await stream.ReadExactlyAsync(paired, cancellationToken).ConfigureAwait(true);
            Assert.True(LobbyRelayProtocol.IsPairedPreamble(paired));
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task WaitForRemoteAsync(
        LobbyNetplayTransport host,
        LobbyNetplayTransport joined,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            _ = host.TryReceive(buffer, out _);
            _ = joined.TryReceive(buffer, out _);
            if (host.HasRemote && joined.HasRemote)
            {
                return;
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task SendUntilReceivedAsync(
        LobbyNetplayTransport sender,
        LobbyNetplayTransport receiver,
        byte[] payload,
        byte[] receiverBuffer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            sender.Send(payload);
            if (receiver.TryReceive(receiverBuffer, out int length)
                && receiverBuffer.AsSpan(0, length).SequenceEqual(payload))
            {
                return;
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }
    }
}
