using System;
using System.Buffers.Binary;

namespace FiveDo.Lobby.Protocol;

/// <summary>
/// The small pre-handshake used on the TCP control relay before emulator bytes flow.
/// </summary>
/// <remarks>
/// Each peer sends a 32-byte connect header. The lobby validates the room token and
/// slot, pairs a host with a join, and sends an 8-byte paired preamble to both. After
/// that point the relay is byte-transparent.
/// </remarks>
public static class LobbyRelayProtocol
{
    /// <summary>The size of the client-to-relay connect header.</summary>
    public const int ConnectSize = 32;

    /// <summary>The size of the relay-to-client paired preamble.</summary>
    public const int PairedSize = 8;

    /// <summary>The preamble sent to both sides after a successful pairing.</summary>
    public static ReadOnlyMemory<byte> PairedPreamble { get; } = new byte[]
    {
        (byte)'5', (byte)'D', (byte)'L', (byte)'P', LobbyProtocol.Version, 0, 0, 0,
    };

    /// <summary>Encodes a relay connect header.</summary>
    /// <param name="destination">Destination span of at least <see cref="ConnectSize"/> bytes.</param>
    /// <param name="roomId">Opaque room identifier.</param>
    /// <param name="token">Room slot token.</param>
    /// <param name="slot">Player slot.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when the destination is too small.</exception>
    public static int EncodeConnect(Span<byte> destination, ulong roomId, Guid token, byte slot)
    {
        if (destination.Length < ConnectSize)
        {
            throw new ArgumentException(
                $"The destination needs at least {ConnectSize} bytes.",
                nameof(destination));
        }

        LobbyProtocol.RelayConnectMagic.CopyTo(destination);
        destination[4] = LobbyProtocol.Version;
        destination[5] = slot;
        destination[6] = 0;
        destination[7] = 0;
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], roomId);
        _ = token.TryWriteBytes(destination[16..32]);
        return ConnectSize;
    }

    /// <summary>Attempts to decode a relay connect header.</summary>
    /// <param name="packet">Candidate header.</param>
    /// <param name="roomId">Decoded room identifier.</param>
    /// <param name="token">Decoded room slot token.</param>
    /// <param name="slot">Decoded player slot.</param>
    /// <returns><see langword="true"/> when the header is valid.</returns>
    public static bool TryDecodeConnect(
        ReadOnlySpan<byte> packet,
        out ulong roomId,
        out Guid token,
        out byte slot)
    {
        roomId = 0;
        token = default;
        slot = 0;
        if (packet.Length != ConnectSize
            || !packet[..4].SequenceEqual(LobbyProtocol.RelayConnectMagic)
            || packet[4] != LobbyProtocol.Version)
        {
            return false;
        }

        slot = packet[5];
        if (slot >= LobbyProtocol.PlayerSlots)
        {
            return false;
        }

        roomId = BinaryPrimitives.ReadUInt64LittleEndian(packet[8..]);
        token = new Guid(packet.Slice(16, 16));
        return roomId != 0;
    }

    /// <summary>Checks whether a packet is the paired preamble.</summary>
    /// <param name="packet">Candidate preamble.</param>
    /// <returns><see langword="true"/> when the preamble matches.</returns>
    public static bool IsPairedPreamble(ReadOnlySpan<byte> packet)
    {
        return packet.Length == PairedSize
            && packet[..4].SequenceEqual(LobbyProtocol.RelayPairedMagic)
            && packet[4] == LobbyProtocol.Version;
    }
}
