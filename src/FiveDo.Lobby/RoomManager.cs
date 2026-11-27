using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using FiveDo.Lobby.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FiveDo.Lobby;

/// <summary>The result of a successful room join.</summary>
/// <param name="Room">The joined room.</param>
/// <param name="SlotToken">The joining slot token.</param>
/// <param name="Slot">The assigned zero-based slot.</param>
public readonly record struct RoomJoin(Room Room, string SlotToken, int Slot);

/// <summary>
/// Thread-safe in-memory room registry. Rooms are deliberately ephemeral; the service
/// does not persist join codes, tokens, or endpoints.
/// </summary>
public sealed class RoomManager
{
    private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const int CodeLength = 6;
    private readonly ConcurrentDictionary<string, Room> _roomsByCode = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ulong, Room> _roomsById = [];
    private readonly Lock _createGate = new();
    private readonly LobbyOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RoomManager> _logger;

    /// <summary>Initializes a new room registry.</summary>
    /// <param name="options">Lobby options.</param>
    /// <param name="loggerFactory">Logger factory for room relays.</param>
    public RoomManager(IOptions<LobbyOptions> options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _options = options.Value;
        _options.Validate();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<RoomManager>();
    }

    /// <summary>Gets the number of live rooms.</summary>
    public int Count => _roomsByCode.Count;

    /// <summary>Gets the maximum number of live rooms.</summary>
    public int MaxRooms => _options.MaxRooms;

    /// <summary>Creates a room.</summary>
    /// <param name="request">Room metadata.</param>
    /// <param name="now">Current UTC time.</param>
    /// <param name="clientIp">Client address used for the per-IP quota.</param>
    /// <returns>The new room.</returns>
    /// <exception cref="LobbyApiException">Thrown when the request is invalid or the lobby is full.</exception>
    public Room CreateRoom(CreateRoomRequest request, DateTimeOffset now, string? clientIp = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateRequest(request);
        lock (_createGate)
        {
            if (_roomsByCode.Count >= _options.MaxRooms)
            {
                throw new LobbyApiException(
                    HttpStatusCode.ServiceUnavailable,
                    "lobby_full",
                    "The lobby has reached its room limit.");
            }

            if (!string.IsNullOrWhiteSpace(clientIp)
                && _roomsByCode.Values.Count(
                    room => string.Equals(room.CreatedByIp, clientIp, StringComparison.Ordinal))
                    >= _options.MaxRoomsPerIp)
            {
                throw new LobbyApiException(
                    HttpStatusCode.TooManyRequests,
                    "room_quota",
                    "This client has reached its live-room limit.");
            }

            string code = CreateUniqueCode();
            ulong roomId = CreateUniqueRoomId();
            string hostToken = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            var room = new Room(
                code,
                roomId,
                hostToken,
                request,
                now,
                TimeSpan.FromMinutes(_options.RoomLifetimeMinutes),
                TimeSpan.FromMinutes(_options.RoomIdleTimeoutMinutes),
                clientIp);
            TcpRelay? controlRelay = null;
            UdpRelay? udpRelay = null;
            try
            {
                if (_options.EnableRelay)
                {
                    controlRelay = new TcpRelay(
                        room,
                        _options.ControlRelayPortStart,
                        _options.ControlRelayPortEnd,
                        _options.MaxControlRelayMinutes,
                        _loggerFactory.CreateLogger<TcpRelay>());
                    udpRelay = new UdpRelay(
                        room,
                        _options.UdpRelayPortStart,
                        _options.UdpRelayPortEnd,
                        _options.MaxUdpRelayBytesPerSecond,
                        _loggerFactory.CreateLogger<UdpRelay>());
                }

                room.AttachRelays(controlRelay, udpRelay);
                if (!_roomsByCode.TryAdd(code, room) || !_roomsById.TryAdd(roomId, room))
                {
                    throw new LobbyApiException(
                        HttpStatusCode.ServiceUnavailable,
                        "room_allocation_failed",
                        "A room identifier collision occurred; retry the request.");
                }
            }
            catch
            {
                _roomsByCode.TryRemove(code, out _);
                _roomsById.TryRemove(roomId, out _);
                controlRelay?.Dispose();
                udpRelay?.Dispose();
                room.Dispose();
                throw;
            }

            LobbyLog.RoomCreated(_logger, code, request.Emulator, request.ContentId);
            return room;
        }
    }

    /// <summary>Joins a room by join code.</summary>
    /// <param name="joinCode">Join code.</param>
    /// <param name="request">Join metadata.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The joined room and slot token.</returns>
    /// <exception cref="LobbyApiException">Thrown when the room is missing, incompatible, or full.</exception>
    public RoomJoin Join(string joinCode, JoinRoomRequest request, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(joinCode);
        ArgumentNullException.ThrowIfNull(request);
        ValidateJoinRequest(request);
        if (!TryGetRoom(joinCode, out Room room))
        {
            throw new LobbyApiException(
                HttpStatusCode.NotFound,
                "room_not_found",
                "The join code was not found.");
        }

        if (room.IsExpired(now))
        {
            RemoveRoom(room);
            throw new LobbyApiException(
                HttpStatusCode.NotFound,
                "room_expired",
                "The join code has expired.");
        }

        if (!string.Equals(room.Emulator, request.Emulator, StringComparison.Ordinal))
        {
            throw new LobbyApiException(
                HttpStatusCode.Conflict,
                "emulator_mismatch",
                $"The room is for {room.Emulator}, not {request.Emulator}.");
        }

        if (room.ProtocolVersion != request.ProtocolVersion)
        {
            throw new LobbyApiException(
                HttpStatusCode.Conflict,
                "protocol_mismatch",
                $"The room uses protocol version {room.ProtocolVersion}.");
        }

        if (!string.Equals(room.ContentId, request.ContentId, StringComparison.Ordinal))
        {
            throw new LobbyApiException(
                HttpStatusCode.Conflict,
                "content_mismatch",
                "The room content identity does not match.");
        }

        if (!string.IsNullOrWhiteSpace(room.ContentHash)
            && !string.IsNullOrWhiteSpace(request.ContentHash)
            && !string.Equals(room.ContentHash, request.ContentHash, StringComparison.Ordinal))
        {
            throw new LobbyApiException(
                HttpStatusCode.Conflict,
                "content_mismatch",
                "The room content fingerprint does not match.");
        }

        room.Touch(now);
        if (!room.TryJoin(request, now, out string slotToken, out int slot, out string? error))
        {
            throw new LobbyApiException(
                HttpStatusCode.Conflict,
                "room_full",
                error ?? "The room is full.");
        }

        LobbyLog.RoomJoined(_logger, room.JoinCode, slot);
        return new RoomJoin(room, slotToken, slot);
    }

    /// <summary>Gets a room by join code.</summary>
    /// <param name="joinCode">Join code.</param>
    /// <param name="room">Matched room.</param>
    /// <returns><see langword="true"/> when found.</returns>
    public bool TryGetRoom(string joinCode, out Room room)
    {
        if (!string.IsNullOrWhiteSpace(joinCode)
            && _roomsByCode.TryGetValue(joinCode.Trim().ToUpperInvariant(), out Room? found))
        {
            room = found;
            return true;
        }

        room = null!;
        return false;
    }

    /// <summary>Gets a room by its UDP identifier.</summary>
    /// <param name="roomId">UDP room identifier.</param>
    /// <param name="room">Matched room.</param>
    /// <returns><see langword="true"/> when found.</returns>
    public bool TryGetRoom(ulong roomId, out Room room)
    {
        if (_roomsById.TryGetValue(roomId, out Room? found))
        {
            room = found;
            return true;
        }

        room = null!;
        return false;
    }

    /// <summary>Authenticates a room token.</summary>
    /// <param name="joinCode">Join code.</param>
    /// <param name="token">Slot token.</param>
    /// <param name="room">Matched room.</param>
    /// <param name="slot">Authenticated slot.</param>
    /// <returns><see langword="true"/> when the token is valid.</returns>
    public bool TryAuthenticate(string joinCode, string token, out Room room, out int slot)
    {
        slot = -1;
        return TryGetRoom(joinCode, out room) && room.TryAuthenticate(token, out slot);
    }

    /// <summary>Closes a room using the host token.</summary>
    /// <param name="joinCode">Join code.</param>
    /// <param name="hostToken">Host token.</param>
    /// <returns><see langword="true"/> when the room was closed.</returns>
    /// <exception cref="LobbyApiException">Thrown when the token is not the host token.</exception>
    public bool CloseRoom(string joinCode, string hostToken)
    {
        if (!TryGetRoom(joinCode, out Room room))
        {
            return false;
        }

        if (!room.TryAuthenticate(hostToken, out int slot) || slot != 0)
        {
            throw new LobbyApiException(
                HttpStatusCode.Forbidden,
                "unauthorized",
                "Only the host token can close the room.");
        }

        return RemoveRoom(room);
    }

    /// <summary>Removes expired rooms.</summary>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The number of removed rooms.</returns>
    public int RemoveExpired(DateTimeOffset now)
    {
        var removed = 0;
        foreach (Room room in _roomsByCode.Values)
        {
            if (!room.IsExpired(now))
            {
                continue;
            }

            if (RemoveRoom(room))
            {
                removed++;
            }
        }

        return removed;
    }

    private bool RemoveRoom(Room room)
    {
        bool removed = _roomsByCode.TryRemove(room.JoinCode, out _);
        _ = _roomsById.TryRemove(room.UdpRoomId, out _);
        if (removed)
        {
            room.Close();
        }

        return removed;
    }

    private string CreateUniqueCode()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            string code = string.Create(
                CodeLength,
                0,
                static (span, _) =>
                {
                    for (var i = 0; i < span.Length; i++)
                    {
                        span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
                    }
                });
            if (!_roomsByCode.ContainsKey(code))
            {
                return code;
            }
        }

        throw new LobbyApiException(
            HttpStatusCode.ServiceUnavailable,
            "room_allocation_failed",
            "Could not allocate a unique join code.");
    }

    private ulong CreateUniqueRoomId()
    {
        Span<byte> bytes = stackalloc byte[8];
        for (var attempt = 0; attempt < 32; attempt++)
        {
            RandomNumberGenerator.Fill(bytes);
            ulong id = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
            if (id != 0 && !_roomsById.ContainsKey(id))
            {
                return id;
            }
        }

        throw new LobbyApiException(
            HttpStatusCode.ServiceUnavailable,
            "room_allocation_failed",
            "Could not allocate a unique room identifier.");
    }

    private static void ValidateCreateRequest(CreateRoomRequest request)
    {
        ValidateMetadata(
            request.Emulator,
            request.ProtocolVersion,
            request.ContentId,
            request.ContentHash,
            request.DisplayName,
            request.MaxPlayers);
    }

    private static void ValidateJoinRequest(JoinRoomRequest request)
    {
        ValidateMetadata(
            request.Emulator,
            request.ProtocolVersion,
            request.ContentId,
            request.ContentHash,
            request.DisplayName,
            LobbyProtocol.PlayerSlots);
    }

    private static void ValidateMetadata(
        string? emulator,
        int protocolVersion,
        string? contentId,
        string? contentHash,
        string? displayName,
        int maxPlayers)
    {
        if (string.IsNullOrWhiteSpace(emulator) || emulator.Length > 32)
        {
            throw new LobbyApiException(
                HttpStatusCode.BadRequest,
                "invalid_request",
                "emulator must be 1-32 characters.");
        }

        if (protocolVersion <= 0)
        {
            throw new LobbyApiException(
                HttpStatusCode.BadRequest,
                "invalid_request",
                "protocolVersion must be positive.");
        }

        if (string.IsNullOrWhiteSpace(contentId) || contentId.Length > 256)
        {
            throw new LobbyApiException(
                HttpStatusCode.BadRequest,
                "invalid_request",
                "contentId must be 1-256 characters.");
        }

        if (contentHash is { Length: > 256 })
        {
            throw new LobbyApiException(
                HttpStatusCode.BadRequest,
                "invalid_request",
                "contentHash must be at most 256 characters.");
        }

        if (displayName is { Length: > 64 })
        {
            throw new LobbyApiException(
                HttpStatusCode.BadRequest,
                "invalid_request",
                "displayName must be at most 64 characters.");
        }

        if (maxPlayers != LobbyProtocol.PlayerSlots)
        {
            throw new LobbyApiException(
                HttpStatusCode.BadRequest,
                "invalid_request",
                $"The prototype supports exactly {LobbyProtocol.PlayerSlots} players.");
        }
    }
}
