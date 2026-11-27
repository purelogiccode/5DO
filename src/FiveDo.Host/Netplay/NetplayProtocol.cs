using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using FiveDo.Input;

namespace FiveDo.Host.Netplay;

/// <summary>
/// Message kinds carried by the versioned netplay datagram format.
/// </summary>
public enum NetplayMessageType : byte
{
    /// <summary>Sent by the joining peer to open a session.</summary>
    Hello = 1,

    /// <summary>Sent by the hosting peer to accept a session.</summary>
    HelloAck = 2,

    /// <summary>Carries one peer's redundant input history.</summary>
    Input = 3,

    /// <summary>Carries a frame and cumulative audio hash checkpoint.</summary>
    Hash = 4,

    /// <summary>Closes the session and reports why.</summary>
    Bye = 5,

    /// <summary>Requests a resend of a specific input frame.</summary>
    InputRequest = 6,
}

/// <summary>
/// The peer's role in a directly connected session.
/// </summary>
public enum NetplayRole : byte
{
    /// <summary>The hosting peer, which owns player 1 and the listening port.</summary>
    Host = 0,

    /// <summary>The joining peer, which owns player 2.</summary>
    Join = 1,
}

/// <summary>
/// Why a peer closed a netplay session.
/// </summary>
public enum NetplayByeReason : byte
{
    /// <summary>The requested frame count completed.</summary>
    Normal = 0,

    /// <summary>The frame or audio hash diverged from the peer.</summary>
    Desync = 1,

    /// <summary>The content hash or protocol version did not match.</summary>
    ContentMismatch = 2,

    /// <summary>A required packet did not arrive before the deadline.</summary>
    Timeout = 3,

    /// <summary>A malformed packet was received.</summary>
    ProtocolError = 4,

    /// <summary>The remote peer stopped the session.</summary>
    PeerStopped = 5,
}

/// <summary>
/// The opening handshake payload. The join request uses session id zero and adopts
/// the hosting peer's id from the acknowledgement.
/// </summary>
/// <param name="SessionId">Host-generated session identifier.</param>
/// <param name="ContentHash">Hash of the BIOS, disc identity, and emulator tuning.</param>
/// <param name="FirstFrame">First emulated frame in the session.</param>
/// <param name="Role">Sender role.</param>
/// <param name="InputDelay">Input lead in emulated frames.</param>
/// <param name="PlayerCount">Connected player count.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayHello(
    ulong SessionId,
    ulong ContentHash,
    long FirstFrame,
    NetplayRole Role,
    byte InputDelay,
    byte PlayerCount);

/// <summary>
/// One frame of a peer's local controller input.
/// </summary>
/// <param name="Frame">Emulated frame the input applies to.</param>
/// <param name="Buttons">Pressed buttons.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayInputEntry(long Frame, ControllerButton Buttons);

/// <summary>
/// A frame checkpoint used for desync detection.
/// </summary>
/// <param name="Frame">Emulated frame that was just executed.</param>
/// <param name="FrameHash">Deterministic frame hash.</param>
/// <param name="AudioHash">Cumulative PCM hash.</param>
/// <param name="AudioSamples">Cumulative stereo sample count.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayHash(
    long Frame,
    uint FrameHash,
    uint AudioHash,
    long AudioSamples);

/// <summary>
/// Serializes and validates the fixed netplay message set.
/// </summary>
/// <remarks>
/// Every datagram starts with an eight-byte header: a four-byte magic value, one
/// protocol-version byte, one message-type byte, and a little-endian payload length.
/// All multi-byte payload fields are little-endian, so packets are independent of the
/// host architecture.
/// </remarks>
public static class NetplayCodec
{
    /// <summary>The protocol version written by this build.</summary>
    public const byte Version = 1;

    /// <summary>The largest datagram the protocol accepts.</summary>
    public const int MaxPacketSize = 256;

    /// <summary>The number of input entries carried by one redundant packet.</summary>
    public const int InputRedundancy = 8;

    /// <summary>The fixed header size in bytes.</summary>
    public const int HeaderSize = 8;

    private const uint Magic = 0x504E4435; // "5DNP" in little-endian byte order.
    private const int HelloPayloadSize = 28;
    private const int HashPayloadSize = 24;
    private const int InputHeaderSize = 4;
    private const int InputEntrySize = 12;

    /// <summary>Writes a hello or hello-acknowledgement packet.</summary>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <param name="type">Hello or hello acknowledgement.</param>
    /// <param name="hello">Handshake payload.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when the message type is not a handshake.</exception>
    public static int WriteHello(Span<byte> destination, NetplayMessageType type, in NetplayHello hello)
    {
        if (type is not (NetplayMessageType.Hello or NetplayMessageType.HelloAck))
        {
            throw new ArgumentException("The message type is not a handshake.", nameof(type));
        }

        RequireCapacity(destination, HelloPayloadSize);
        WriteHeader(destination, type, HelloPayloadSize);
        Span<byte> payload = destination.Slice(HeaderSize, HelloPayloadSize);
        BinaryPrimitives.WriteUInt64LittleEndian(payload, hello.SessionId);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[8..], hello.ContentHash);
        BinaryPrimitives.WriteInt64LittleEndian(payload[16..], hello.FirstFrame);
        payload[24] = (byte)hello.Role;
        payload[25] = hello.InputDelay;
        payload[26] = hello.PlayerCount;
        payload[27] = 0;
        return HeaderSize + HelloPayloadSize;
    }

    /// <summary>Reads a hello or hello-acknowledgement packet.</summary>
    /// <param name="packet">Complete datagram.</param>
    /// <param name="type">Expected message type.</param>
    /// <param name="hello">Decoded handshake payload.</param>
    /// <returns><see langword="true"/> when the packet is a valid handshake.</returns>
    public static bool TryReadHello(
        ReadOnlySpan<byte> packet,
        NetplayMessageType type,
        out NetplayHello hello)
    {
        hello = default;
        if (!TryReadPayload(packet, type, HelloPayloadSize, out ReadOnlySpan<byte> payload))
        {
            return false;
        }

        var sessionId = BinaryPrimitives.ReadUInt64LittleEndian(payload);
        var contentHash = BinaryPrimitives.ReadUInt64LittleEndian(payload[8..]);
        var firstFrame = BinaryPrimitives.ReadInt64LittleEndian(payload[16..]);
        var role = (NetplayRole)payload[24];
        if (role is not (NetplayRole.Host or NetplayRole.Join))
        {
            return false;
        }

        hello = new NetplayHello(
            sessionId,
            contentHash,
            firstFrame,
            role,
            payload[25],
            payload[26]);
        return true;
    }

    /// <summary>Writes a redundant input packet.</summary>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <param name="player">Sending player index, zero-based.</param>
    /// <param name="entries">Frame-ordered input history.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when the history is empty or too long.</exception>
    public static int WriteInput(
        Span<byte> destination,
        byte player,
        ReadOnlySpan<NetplayInputEntry> entries)
    {
        if (entries.Length is 0 or > InputRedundancy)
        {
            throw new ArgumentException(
                $"Input packets require 1-{InputRedundancy} entries.",
                nameof(entries));
        }

        var payloadSize = InputHeaderSize + (entries.Length * InputEntrySize);
        RequireCapacity(destination, payloadSize);
        WriteHeader(destination, NetplayMessageType.Input, payloadSize);
        Span<byte> payload = destination.Slice(HeaderSize, payloadSize);
        payload[0] = player;
        payload[1] = (byte)entries.Length;
        payload[2] = 0;
        payload[3] = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            Span<byte> entry = payload.Slice(InputHeaderSize + (i * InputEntrySize), InputEntrySize);
            BinaryPrimitives.WriteInt64LittleEndian(entry, entries[i].Frame);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[8..], (ushort)entries[i].Buttons);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[10..], 0);
        }

        return HeaderSize + payloadSize;
    }

    /// <summary>Reads a redundant input packet.</summary>
    /// <param name="packet">Complete datagram.</param>
    /// <param name="player">Sending player index.</param>
    /// <param name="entries">Decoded input history.</param>
    /// <param name="count">Number of decoded entries.</param>
    /// <returns><see langword="true"/> when the packet is a valid input message.</returns>
    public static bool TryReadInput(
        ReadOnlySpan<byte> packet,
        out byte player,
        Span<NetplayInputEntry> entries,
        out int count)
    {
        player = 0;
        count = 0;
        if (!TryReadHeader(packet, NetplayMessageType.Input, out ReadOnlySpan<byte> payload)
            || payload.Length < InputHeaderSize)
        {
            return false;
        }

        var entryCount = payload[1];
        if (entryCount is 0 or > InputRedundancy
            || payload.Length != InputHeaderSize + (entryCount * InputEntrySize)
            || entries.Length < entryCount)
        {
            return false;
        }

        player = payload[0];
        for (var i = 0; i < entryCount; i++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(InputHeaderSize + (i * InputEntrySize), InputEntrySize);
            entries[i] = new NetplayInputEntry(
                BinaryPrimitives.ReadInt64LittleEndian(entry),
                (ControllerButton)BinaryPrimitives.ReadUInt16LittleEndian(entry[8..]));
        }

        count = entryCount;
        return true;
    }

    /// <summary>Writes a hash checkpoint packet.</summary>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <param name="hash">Checkpoint values.</param>
    /// <returns>The number of bytes written.</returns>
    public static int WriteHash(Span<byte> destination, in NetplayHash hash)
    {
        RequireCapacity(destination, HashPayloadSize);
        WriteHeader(destination, NetplayMessageType.Hash, HashPayloadSize);
        Span<byte> payload = destination.Slice(HeaderSize, HashPayloadSize);
        BinaryPrimitives.WriteInt64LittleEndian(payload, hash.Frame);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[8..], hash.FrameHash);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[12..], hash.AudioHash);
        BinaryPrimitives.WriteInt64LittleEndian(payload[16..], hash.AudioSamples);
        return HeaderSize + HashPayloadSize;
    }

    /// <summary>Reads a hash checkpoint packet.</summary>
    /// <param name="packet">Complete datagram.</param>
    /// <param name="hash">Decoded checkpoint.</param>
    /// <returns><see langword="true"/> when the packet is a valid hash message.</returns>
    public static bool TryReadHash(ReadOnlySpan<byte> packet, out NetplayHash hash)
    {
        hash = default;
        if (!TryReadPayload(packet, NetplayMessageType.Hash, HashPayloadSize, out ReadOnlySpan<byte> payload))
        {
            return false;
        }

        hash = new NetplayHash(
            BinaryPrimitives.ReadInt64LittleEndian(payload),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]),
            BinaryPrimitives.ReadInt64LittleEndian(payload[16..]));
        return true;
    }

    /// <summary>Writes a request for a specific input frame.</summary>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <param name="frame">Missing frame number.</param>
    /// <returns>The number of bytes written.</returns>
    public static int WriteInputRequest(Span<byte> destination, long frame)
    {
        RequireCapacity(destination, 8);
        WriteHeader(destination, NetplayMessageType.InputRequest, 8);
        BinaryPrimitives.WriteInt64LittleEndian(destination[HeaderSize..], frame);
        return HeaderSize + 8;
    }

    /// <summary>Reads a request for a specific input frame.</summary>
    /// <param name="packet">Complete datagram.</param>
    /// <param name="frame">Missing frame number.</param>
    /// <returns><see langword="true"/> when the packet is a valid input request.</returns>
    public static bool TryReadInputRequest(ReadOnlySpan<byte> packet, out long frame)
    {
        frame = 0;
        if (!TryReadPayload(packet, NetplayMessageType.InputRequest, 8, out ReadOnlySpan<byte> payload))
        {
            return false;
        }

        frame = BinaryPrimitives.ReadInt64LittleEndian(payload);
        return frame >= 0;
    }

    /// <summary>Writes a session-close packet.</summary>
    /// <param name="destination">Destination span of at least <see cref="MaxPacketSize"/> bytes.</param>
    /// <param name="reason">Close reason.</param>
    /// <returns>The number of bytes written.</returns>
    public static int WriteBye(Span<byte> destination, NetplayByeReason reason)
    {
        RequireCapacity(destination, 1);
        WriteHeader(destination, NetplayMessageType.Bye, 1);
        destination[HeaderSize] = (byte)reason;
        return HeaderSize + 1;
    }

    /// <summary>Reads a session-close packet.</summary>
    /// <param name="packet">Complete datagram.</param>
    /// <param name="reason">Decoded close reason.</param>
    /// <returns><see langword="true"/> when the packet is a valid close message.</returns>
    public static bool TryReadBye(ReadOnlySpan<byte> packet, out NetplayByeReason reason)
    {
        reason = default;
        if (!TryReadPayload(packet, NetplayMessageType.Bye, 1, out ReadOnlySpan<byte> payload))
        {
            return false;
        }

        reason = (NetplayByeReason)payload[0];
        return reason <= NetplayByeReason.PeerStopped;
    }

    /// <summary>Reads the message type from any valid protocol header.</summary>
    /// <param name="packet">Candidate datagram.</param>
    /// <param name="type">Decoded message type.</param>
    /// <returns><see langword="true"/> when the header is valid.</returns>
    public static bool TryPeekType(ReadOnlySpan<byte> packet, out NetplayMessageType type)
    {
        type = default;
        if (packet.Length < HeaderSize
            || BinaryPrimitives.ReadUInt32LittleEndian(packet) != Magic
            || packet[4] != Version)
        {
            return false;
        }

        type = (NetplayMessageType)packet[5];
        return type is >= NetplayMessageType.Hello and <= NetplayMessageType.InputRequest;
    }

    private static void WriteHeader(Span<byte> destination, NetplayMessageType type, int payloadSize)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        destination[4] = Version;
        destination[5] = (byte)type;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], (ushort)payloadSize);
    }

    private static bool TryReadPayload(
        ReadOnlySpan<byte> packet,
        NetplayMessageType type,
        int expectedPayloadSize,
        out ReadOnlySpan<byte> payload)
    {
        payload = default;
        if (!TryReadHeader(packet, type, out payload))
        {
            return false;
        }

        return payload.Length == expectedPayloadSize;
    }

    private static bool TryReadHeader(
        ReadOnlySpan<byte> packet,
        NetplayMessageType type,
        out ReadOnlySpan<byte> payload)
    {
        payload = default;
        if (packet.Length < HeaderSize
            || BinaryPrimitives.ReadUInt32LittleEndian(packet) != Magic
            || packet[4] != Version
            || packet[5] != (byte)type)
        {
            return false;
        }

        var payloadSize = BinaryPrimitives.ReadUInt16LittleEndian(packet[6..]);
        if (payloadSize != packet.Length - HeaderSize)
        {
            return false;
        }

        payload = packet[HeaderSize..];
        return true;
    }

    private static void RequireCapacity(Span<byte> destination, int payloadSize)
    {
        if (destination.Length < HeaderSize + payloadSize)
        {
            throw new ArgumentException(
                $"The destination needs at least {HeaderSize + payloadSize} bytes.",
                nameof(destination));
        }
    }
}
