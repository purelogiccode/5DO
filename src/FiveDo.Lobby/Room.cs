using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Lobby;

/// <summary>
/// One in-memory lobby room. The room owns player slots, the public UDP endpoint
/// assignments, and the optional TCP/UDP relays. It never inspects emulator payloads.
/// </summary>
public sealed class Room : IDisposable
{
    private readonly Lock _gate = new();
    private readonly RoomSlot[] _slots = [new RoomSlot(), new RoomSlot()];
    private readonly DateTimeOffset _createdAt;
    private readonly TimeSpan _lifetime;
    private readonly TimeSpan _idleTimeout;
    private DateTimeOffset _lastActivity;
    private TcpRelay? _controlRelay;
    private UdpRelay? _udpRelay;
    private bool _closed;

    internal Room(
        string joinCode,
        ulong udpRoomId,
        string hostToken,
        CreateRoomRequest request,
        DateTimeOffset now,
        TimeSpan lifetime,
        TimeSpan idleTimeout,
        string? createdByIp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(joinCode);
        ArgumentNullException.ThrowIfNull(request);
        JoinCode = joinCode;
        UdpRoomId = udpRoomId;
        Emulator = request.Emulator;
        ProtocolVersion = request.ProtocolVersion;
        ContentId = request.ContentId;
        ContentHash = request.ContentHash;
        MaxPlayers = request.MaxPlayers;
        CreatedByIp = createdByIp;
        _createdAt = now;
        _lastActivity = now;
        _lifetime = lifetime;
        _idleTimeout = idleTimeout;
        UdpNonce = CreateNonce();
        _slots[0].Token = ParseToken(hostToken);
        _slots[0].DisplayName = request.DisplayName;
        _slots[0].Joined = true;
    }

    /// <summary>Gets the human join code.</summary>
    public string JoinCode { get; }

    /// <summary>Gets the opaque UDP room identifier.</summary>
    public ulong UdpRoomId { get; }

    /// <summary>Gets the emulator tag.</summary>
    public string Emulator { get; }

    /// <summary>Gets the emulator-facing protocol version.</summary>
    public int ProtocolVersion { get; }

    /// <summary>Gets the opaque content identity.</summary>
    public string ContentId { get; }

    /// <summary>Gets the optional content fingerprint.</summary>
    public string? ContentHash { get; }

    /// <summary>Gets the maximum player count.</summary>
    public int MaxPlayers { get; }

    /// <summary>Gets the client IP that created the room, when known.</summary>
    public string? CreatedByIp { get; }

    /// <summary>Gets the room creation time.</summary>
    public DateTimeOffset CreatedAt => _createdAt;

    /// <summary>Gets the punch/registration nonce for this room.</summary>
    public ulong UdpNonce { get; }

    /// <summary>Gets the secret host token.</summary>
    public string HostToken => _slots[0].Token.GetValueOrDefault().ToString("N", CultureInfo.InvariantCulture);

    /// <summary>Gets the allocated TCP control relay port, or zero when disabled.</summary>
    public int ControlRelayPort => _controlRelay?.Port ?? 0;

    /// <summary>Gets the allocated UDP relay port, or zero when disabled.</summary>
    public int UdpRelayPort => _udpRelay?.Port ?? 0;

    /// <summary>Gets when the room expires.</summary>
    public DateTimeOffset ExpiresAt
    {
        get
        {
            lock (_gate)
            {
                return ComputeExpiresAt();
            }
        }
    }

    /// <summary>Raised when room state changes. Handlers run outside the room lock.</summary>
    public event EventHandler<RoomEventArgs>? Changed;

    /// <summary>Attaches the optional relays allocated for this room.</summary>
    /// <param name="controlRelay">TCP control relay.</param>
    /// <param name="udpRelay">UDP data relay.</param>
    public void AttachRelays(TcpRelay? controlRelay, UdpRelay? udpRelay)
    {
        _controlRelay = controlRelay;
        _udpRelay = udpRelay;
    }

    /// <summary>Refreshes the room's idle deadline.</summary>
    /// <param name="now">Current UTC time.</param>
    public void Touch(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_closed && now > _lastActivity)
            {
                _lastActivity = now;
            }
        }
    }

    /// <summary>Attempts to join or resume the joining player slot.</summary>
    /// <param name="request">Join metadata.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="slotToken">Assigned slot token.</param>
    /// <param name="slot">Assigned zero-based slot.</param>
    /// <param name="error">Failure detail.</param>
    /// <returns><see langword="true"/> when the slot is assigned.</returns>
    public bool TryJoin(
        JoinRoomRequest request,
        DateTimeOffset now,
        out string slotToken,
        out int slot,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(request);
        slotToken = string.Empty;
        slot = -1;
        error = null;
        RoomEventArgs? changed = null;
        lock (_gate)
        {
            if (_closed)
            {
                error = "The room is closed.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.SlotToken)
                && Guid.TryParseExact(request.SlotToken, "N", out Guid resumeToken))
            {
                for (var i = 0; i < MaxPlayers; i++)
                {
                    if (_slots[i].Token == resumeToken)
                    {
                        slot = i;
                        slotToken = resumeToken.ToString("N", CultureInfo.InvariantCulture);
                        _slots[i].DisplayName = request.DisplayName ?? _slots[i].DisplayName;
                        _lastActivity = now;
                        return true;
                    }
                }
            }

            for (var i = 1; i < MaxPlayers; i++)
            {
                if (_slots[i].Token is not null)
                {
                    continue;
                }

                Guid token = Guid.NewGuid();
                _slots[i].Token = token;
                _slots[i].DisplayName = request.DisplayName;
                _slots[i].Joined = true;
                slot = i;
                slotToken = token.ToString("N", CultureInfo.InvariantCulture);
                _lastActivity = now;
                changed = new RoomEventArgs(RoomEventType.Joined, i, null);
                break;
            }

            if (slot < 0)
            {
                error = "The room is full.";
                return false;
            }
        }

        if (changed is not null)
        {
            Notify(changed);
        }

        return true;
    }

    /// <summary>Authenticates a slot token.</summary>
    /// <param name="token">Token text.</param>
    /// <param name="slot">Authenticated slot.</param>
    /// <returns><see langword="true"/> when the token is valid.</returns>
    public bool TryAuthenticate(string token, out int slot)
    {
        slot = -1;
        return Guid.TryParseExact(token, "N", out Guid parsed) && TryAuthenticate(parsed, out slot);
    }

    /// <summary>Authenticates a slot token.</summary>
    /// <param name="token">Token value.</param>
    /// <param name="slot">Authenticated slot.</param>
    /// <returns><see langword="true"/> when the token is valid.</returns>
    public bool TryAuthenticate(Guid token, out int slot)
    {
        slot = -1;
        if (token == Guid.Empty)
        {
            return false;
        }

        lock (_gate)
        {
            for (var i = 0; i < MaxPlayers; i++)
            {
                if (_slots[i].Token == token)
                {
                    slot = i;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Records a player's public UDP rendezvous endpoint.</summary>
    /// <param name="slot">Player slot.</param>
    /// <param name="endpoint">Public endpoint observed by the lobby.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns><see langword="true"/> when the endpoint was accepted.</returns>
    public bool SetUdpEndpoint(int slot, IPEndPoint endpoint, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var changed = false;
        lock (_gate)
        {
            if (_closed || slot < 0 || slot >= MaxPlayers)
            {
                return false;
            }

            if (!EndpointsEqual(_slots[slot].UdpEndPoint, endpoint))
            {
                _slots[slot].UdpEndPoint = endpoint;
                changed = true;
            }

            _lastActivity = now;
        }

        if (changed)
        {
            Notify(new RoomEventArgs(RoomEventType.EndpointUpdated, slot, null));
        }

        return true;
    }

    /// <summary>Clears a player's rendezvous endpoint.</summary>
    /// <param name="slot">Player slot.</param>
    public void ClearUdpEndpoint(int slot)
    {
        lock (_gate)
        {
            _slots[slot].UdpEndPoint = null;
        }
    }

    /// <summary>Records a player's endpoint as observed by the UDP relay.</summary>
    /// <param name="slot">Player slot.</param>
    /// <param name="endpoint">Public endpoint observed by the relay.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns><see langword="true"/> when the endpoint was accepted.</returns>
    public bool SetRelayEndpoint(int slot, IPEndPoint endpoint, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var changed = false;
        lock (_gate)
        {
            if (_closed || slot < 0 || slot >= MaxPlayers)
            {
                return false;
            }

            if (!EndpointsEqual(_slots[slot].RelayEndPoint, endpoint))
            {
                _slots[slot].RelayEndPoint = endpoint;
                changed = true;
            }

            _lastActivity = now;
        }

        if (changed)
        {
            Notify(new RoomEventArgs(RoomEventType.RelayEndpointUpdated, slot, null));
        }

        return true;
    }

    /// <summary>Clears a player's relay endpoint.</summary>
    /// <param name="slot">Player slot.</param>
    public void ClearRelayEndpoint(int slot)
    {
        lock (_gate)
        {
            _slots[slot].RelayEndPoint = null;
        }
    }

    /// <summary>Records whether a signaling socket is connected.</summary>
    /// <param name="slot">Player slot.</param>
    /// <param name="connected">Whether the socket is connected.</param>
    public void SetSignalConnected(int slot, bool connected)
    {
        lock (_gate)
        {
            if (slot >= 0 && slot < MaxPlayers)
            {
                _slots[slot].SignalConnected = connected;
            }
        }
    }

    /// <summary>Records the last reported transport path.</summary>
    /// <param name="slot">Player slot.</param>
    /// <param name="mode">One of <c>auto</c>, <c>direct</c>, or <c>relay</c>.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns><see langword="true"/> when the mode was accepted.</returns>
    public bool SetTransport(int slot, string mode, DateTimeOffset now)
    {
        if (mode is not ("auto" or "direct" or "relay"))
        {
            return false;
        }

        lock (_gate)
        {
            if (_closed || slot < 0 || slot >= MaxPlayers)
            {
                return false;
            }

            _slots[slot].Transport = mode;
            _lastActivity = now;
        }

        Notify(new RoomEventArgs(RoomEventType.TransportChanged, slot, mode));
        return true;
    }

    /// <summary>Records whether a player is ready.</summary>
    /// <param name="slot">Player slot.</param>
    /// <param name="ready">Readiness value.</param>
    public void SetReady(int slot, bool ready)
    {
        lock (_gate)
        {
            if (slot < 0 || slot >= MaxPlayers)
            {
                return;
            }

            _slots[slot].Ready = ready;
        }

        Notify(new RoomEventArgs(RoomEventType.ReadyChanged, slot, ready ? "ready" : "not-ready"));
    }

    /// <summary>Gets the peer's rendezvous endpoint.</summary>
    /// <param name="slot">Requesting slot.</param>
    /// <returns>The peer endpoint, or <see langword="null"/>.</returns>
    public IPEndPoint? GetPeerUdpEndpoint(int slot)
    {
        lock (_gate)
        {
            return GetPeerEndpoint(slot, static value => value.UdpEndPoint);
        }
    }

    /// <summary>Gets the peer's relay endpoint.</summary>
    /// <param name="slot">Requesting slot.</param>
    /// <returns>The peer relay endpoint, or <see langword="null"/>.</returns>
    public IPEndPoint? GetPeerRelayEndpoint(int slot)
    {
        lock (_gate)
        {
            return GetPeerEndpoint(slot, static value => value.RelayEndPoint);
        }
    }

    /// <summary>Finds the slot owning a relay endpoint.</summary>
    /// <param name="endpoint">Observed source endpoint.</param>
    /// <param name="slot">Matched slot.</param>
    /// <returns><see langword="true"/> when a slot matches.</returns>
    public bool TryFindSlotByRelayEndpoint(IPEndPoint endpoint, out int slot)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        lock (_gate)
        {
            for (var i = 0; i < MaxPlayers; i++)
            {
                if (EndpointsEqual(_slots[i].RelayEndPoint, endpoint))
                {
                    slot = i;
                    return true;
                }
            }
        }

        slot = -1;
        return false;
    }

    /// <summary>Gets a public snapshot of the room.</summary>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The room status.</returns>
    public RoomStatus Snapshot(DateTimeOffset now)
    {
        lock (_gate)
        {
            return new RoomStatus
            {
                UdpRoomId = UdpRoomId.ToString("x16", CultureInfo.InvariantCulture),
                JoinCode = JoinCode,
                Emulator = Emulator,
                ProtocolVersion = ProtocolVersion,
                ContentId = ContentId,
                ContentHash = ContentHash,
                MaxPlayers = MaxPlayers,
                Players = CreatePlayerStatuses(),
                CreatedAt = _createdAt,
                ExpiresAt = ComputeExpiresAt(),
                Closed = _closed,
            };
        }
    }

    /// <summary>Creates the connection information shared by both peers.</summary>
    /// <param name="publicHost">Public HTTP/WebSocket host.</param>
    /// <param name="signalPort">HTTP/WebSocket port.</param>
    /// <param name="udpPort">UDP rendezvous port.</param>
    /// <param name="relayHost">
    /// DNS-only host for the TCP/UDP relay endpoints, or <see langword="null"/> to reuse
    /// <paramref name="publicHost"/>.
    /// </param>
    /// <returns>The connection information.</returns>
    public RoomConnectionInfo ToConnectionInfo(
        string publicHost,
        int signalPort,
        int udpPort,
        string? relayHost = null)
    {
        string directHost = string.IsNullOrWhiteSpace(relayHost) ? publicHost : relayHost;
        return new RoomConnectionInfo
        {
            UdpRoomId = UdpRoomId.ToString("x16", CultureInfo.InvariantCulture),
            JoinCode = JoinCode,
            Emulator = Emulator,
            ProtocolVersion = ProtocolVersion,
            ContentId = ContentId,
            ContentHash = ContentHash,
            MaxPlayers = MaxPlayers,
            Signal = new LobbyEndpoint(publicHost, signalPort),
            SignalPath = LobbyProtocol.SignalPath,
            ControlRelay = new LobbyEndpoint(directHost, ControlRelayPort),
            UdpRendezvous = new LobbyEndpoint(directHost, udpPort),
            UdpRelay = new LobbyEndpoint(directHost, UdpRelayPort),
            ExpiresAt = ExpiresAt,
        };
    }

    /// <summary>Checks whether the room has expired or closed.</summary>
    /// <param name="now">Current UTC time.</param>
    /// <returns><see langword="true"/> when the room should be removed.</returns>
    public bool IsExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            return _closed || now >= ComputeExpiresAt();
        }
    }

    /// <summary>Closes the room and disposes its relays.</summary>
    public void Dispose()
    {
        Close();
    }

    /// <summary>Closes the room and notifies signaling clients.</summary>
    public void Close()
    {
        TcpRelay? control;
        UdpRelay? data;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            control = _controlRelay;
            data = _udpRelay;
            _controlRelay = null;
            _udpRelay = null;
        }

        control?.Dispose();
        data?.Dispose();
        Notify(new RoomEventArgs(RoomEventType.Closed, -1, null));
    }

    private static ulong CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        ulong nonce = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return nonce == 0 ? 1UL : nonce;
    }

    private static Guid ParseToken(string token)
    {
        if (!Guid.TryParseExact(token, "N", out Guid parsed) || parsed == Guid.Empty)
        {
            throw new ArgumentException("The host token must be a 32-digit GUID.", nameof(token));
        }

        return parsed;
    }

    private static bool EndpointsEqual(IPEndPoint? left, IPEndPoint? right)
    {
        return left is not null
            && right is not null
            && left.Port == right.Port
            && left.Address.Equals(right.Address);
    }

    private IPEndPoint? GetPeerEndpoint(int slot, Func<RoomSlot, IPEndPoint?> selector)
    {
        for (var offset = 1; offset < MaxPlayers; offset++)
        {
            int candidate = (slot + offset) % MaxPlayers;
            IPEndPoint? endpoint = selector(_slots[candidate]);
            if (endpoint is not null)
            {
                return endpoint;
            }
        }

        return null;
    }

    private RoomPlayerStatus[] CreatePlayerStatuses()
    {
        var players = new RoomPlayerStatus[MaxPlayers];
        for (var i = 0; i < MaxPlayers; i++)
        {
            RoomSlot slot = _slots[i];
            players[i] = new RoomPlayerStatus(
                i,
                slot.DisplayName,
                slot.Joined || slot.SignalConnected || slot.UdpEndPoint is not null || slot.RelayEndPoint is not null,
                slot.Ready,
                slot.Transport);
        }

        return players;
    }

    private DateTimeOffset ComputeExpiresAt()
    {
        DateTimeOffset absolute = _createdAt + _lifetime;
        DateTimeOffset idle = _lastActivity + _idleTimeout;
        return absolute < idle ? absolute : idle;
    }

    private void Notify(RoomEventArgs roomEvent)
    {
        EventHandler<RoomEventArgs>? handlers;
        lock (_gate)
        {
            handlers = Changed;
        }

        handlers?.Invoke(this, roomEvent);
    }

    private sealed class RoomSlot
    {
        public Guid? Token { get; set; }

        public string? DisplayName { get; set; }

        public bool Joined { get; set; }

        public bool Ready { get; set; }

        public bool SignalConnected { get; set; }

        public IPEndPoint? UdpEndPoint { get; set; }

        public IPEndPoint? RelayEndPoint { get; set; }

        public string Transport { get; set; } = "auto";
    }
}
