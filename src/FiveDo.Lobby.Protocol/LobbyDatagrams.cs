using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace FiveDo.Lobby.Protocol;

/// <summary>Kinds of datagrams exchanged with the lobby UDP endpoints.</summary>
public enum LobbyDatagramType : byte
{
    /// <summary>A client registers or refreshes its public UDP endpoint.</summary>
    Register = 1,

    /// <summary>The lobby acknowledges a registration.</summary>
    RegisterAck = 2,

    /// <summary>The lobby tells a client about its peer's public UDP endpoint.</summary>
    Peer = 3,

    /// <summary>A client punches a NAT mapping toward its peer.</summary>
    Punch = 4,

    /// <summary>A client registers with the UDP relay port.</summary>
    RelayHello = 5,

    /// <summary>The relay acknowledges a relay registration.</summary>
    RelayAck = 6,

    /// <summary>A client leaves the room.</summary>
    Bye = 7,

    /// <summary>A client refreshes a rendezvous registration.</summary>
    Keepalive = 8,
}

/// <summary>The requested transport path for a session.</summary>
public enum LobbyTransportMode : byte
{
    /// <summary>Try direct punching first and fall back to the relay.</summary>
    Auto = 0,

    /// <summary>Require a direct punched path.</summary>
    Direct = 1,

    /// <summary>Use the lobby UDP relay immediately.</summary>
    Relay = 2,
}

/// <summary>
/// A fixed-size lobby UDP message. Game traffic is never wrapped in this type; only
/// rendezvous, punch, and relay registration messages use it.
/// </summary>
/// <param name="Type">Datagram kind.</param>
/// <param name="Slot">Sending or receiving player slot.</param>
/// <param name="RoomId">Opaque 64-bit room identifier.</param>
/// <param name="Token">Room slot token, or <see cref="Guid.Empty"/> when not needed.</param>
/// <param name="Nonce">Punch/registration nonce.</param>
/// <param name="PeerEndPoint">Peer public IPv4 endpoint, when known.</param>
/// <param name="RelayPort">UDP relay port, or zero when not applicable.</param>
/// <param name="Mode">Requested or negotiated transport path.</param>
public readonly record struct LobbyDatagram(
    LobbyDatagramType Type,
    byte Slot,
    ulong RoomId,
    Guid Token,
    ulong Nonce,
    IPEndPoint? PeerEndPoint,
    int RelayPort,
    LobbyTransportMode Mode);

/// <summary>
/// Encodes and validates the fixed lobby UDP datagram format.
/// </summary>
/// <remarks>
/// The format is 48 bytes and IPv4-only:
/// <code>
/// 0  magic "5DOL"
/// 4  version
/// 5  type
/// 6  slot
/// 7  mode
/// 8  room id (little-endian)
/// 16 token (16 bytes)
/// 32 nonce (little-endian)
/// 40 peer IPv4 address (network order)
/// 44 peer port (little-endian)
/// 46 relay port (little-endian)
/// </code>
/// A peer address of 0.0.0.0 with port 0 means "not present".
/// </remarks>
public static class LobbyDatagramCodec
{
    /// <summary>The encoded datagram size in bytes.</summary>
    public const int Size = 48;

    /// <summary>Encodes a lobby datagram.</summary>
    /// <param name="destination">Destination span of at least <see cref="Size"/> bytes.</param>
    /// <param name="datagram">Datagram to encode.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when the destination is too small or the endpoint is IPv6.</exception>
    public static int Encode(Span<byte> destination, in LobbyDatagram datagram)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException($"The destination needs at least {Size} bytes.", nameof(destination));
        }

        byte typeValue = (byte)datagram.Type;
        if (typeValue is < (byte)LobbyDatagramType.Register or > (byte)LobbyDatagramType.Keepalive)
        {
            throw new ArgumentException("The lobby datagram type is not recognized.", nameof(datagram));
        }

        byte modeValue = (byte)datagram.Mode;
        if (modeValue > (byte)LobbyTransportMode.Relay)
        {
            throw new ArgumentException("The lobby transport mode is not recognized.", nameof(datagram));
        }

        if (datagram.RelayPort is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(datagram), "The relay port is out of range.");
        }

        byte[] peerAddress = new byte[4];
        var peerPort = 0;
        if (datagram.PeerEndPoint is { } peer)
        {
            if (peer.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("The lobby datagram supports IPv4 endpoints only.", nameof(datagram));
            }

            peer.Address.GetAddressBytes().CopyTo(peerAddress, 0);
            peerPort = peer.Port;
        }

        LobbyProtocol.DatagramMagic.CopyTo(destination);
        destination[4] = LobbyProtocol.Version;
        destination[5] = (byte)datagram.Type;
        destination[6] = datagram.Slot;
        destination[7] = (byte)datagram.Mode;
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], datagram.RoomId);
        _ = datagram.Token.TryWriteBytes(destination[16..32]);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[32..], datagram.Nonce);
        peerAddress.CopyTo(destination[40..44]);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[44..], (ushort)peerPort);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[46..], (ushort)datagram.RelayPort);
        return Size;
    }

    /// <summary>Attempts to decode a lobby datagram.</summary>
    /// <param name="packet">Candidate datagram.</param>
    /// <param name="datagram">Decoded datagram.</param>
    /// <returns><see langword="true"/> when the packet is a valid lobby datagram.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> packet, out LobbyDatagram datagram)
    {
        datagram = default;
        if (!IsLobbyDatagram(packet) || packet.Length != Size)
        {
            return false;
        }

        if (packet[5] is < (byte)LobbyDatagramType.Register or > (byte)LobbyDatagramType.Keepalive
            || packet[7] is > (byte)LobbyTransportMode.Relay)
        {
            return false;
        }

        var type = (LobbyDatagramType)packet[5];
        var mode = (LobbyTransportMode)packet[7];

        var roomId = BinaryPrimitives.ReadUInt64LittleEndian(packet[8..]);
        var token = new Guid(packet.Slice(16, 16));
        var nonce = BinaryPrimitives.ReadUInt64LittleEndian(packet[32..]);
        var peerAddress = packet.Slice(40, 4);
        var peerPort = BinaryPrimitives.ReadUInt16LittleEndian(packet[44..]);
        var relayPort = BinaryPrimitives.ReadUInt16LittleEndian(packet[46..]);
        IPEndPoint? peerEndPoint = null;
        if (peerPort != 0)
        {
            peerEndPoint = new IPEndPoint(new IPAddress(peerAddress), peerPort);
        }

        datagram = new LobbyDatagram(
            type,
            packet[6],
            roomId,
            token,
            nonce,
            peerEndPoint,
            relayPort,
            mode);
        return true;
    }

    /// <summary>Checks whether a packet starts with the lobby magic and version.</summary>
    /// <param name="packet">Candidate datagram.</param>
    /// <returns><see langword="true"/> when the header is a lobby header.</returns>
    public static bool IsLobbyDatagram(ReadOnlySpan<byte> packet)
    {
        return packet.Length >= 6
            && packet[..4].SequenceEqual(LobbyProtocol.DatagramMagic)
            && packet[4] == LobbyProtocol.Version;
    }
}
