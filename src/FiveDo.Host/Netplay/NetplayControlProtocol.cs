using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FiveDo.Host.Netplay;

/// <summary>
/// Control-channel message kinds exchanged over the reliable TCP channel.
/// </summary>
public enum NetplayControlMessageType : byte
{
    /// <summary>The joining peer asks to start or resume a session.</summary>
    JoinRequest = 1,

    /// <summary>The hosting peer accepts or rejects a request.</summary>
    JoinResponse = 2,

    /// <summary>The hosting peer sends a compressed machine state.</summary>
    State = 3,

    /// <summary>The joining peer acknowledges the state and its frame hash.</summary>
    StateAck = 4,

    /// <summary>Either peer closes the control channel.</summary>
    Close = 5,
}

/// <summary>
/// Whether a join request starts a new session or resumes an existing one.
/// </summary>
public enum NetplayJoinKind : byte
{
    /// <summary>A first connection.</summary>
    Fresh = 0,

    /// <summary>A reconnect after a dropped session.</summary>
    Reconnect = 1,
}

/// <summary>
/// Control-channel acceptance and failure reasons.
/// </summary>
public enum NetplayControlReason : byte
{
    /// <summary>The request was accepted.</summary>
    Accepted = 0,

    /// <summary>The BIOS, disc, tuning, or protocol version does not match.</summary>
    ContentMismatch = 1,

    /// <summary>Another peer is already connected.</summary>
    Busy = 2,

    /// <summary>A malformed control message was received.</summary>
    ProtocolError = 3,

    /// <summary>A control operation exceeded its deadline.</summary>
    Timeout = 4,

    /// <summary>The peer closed the channel.</summary>
    PeerClosed = 5,

    /// <summary>The transferred state failed validation.</summary>
    StateInvalid = 6,

    /// <summary>The requested operation is not supported by this build.</summary>
    Unsupported = 7,

    /// <summary>The session was stopped locally.</summary>
    LocalStop = 8,
}

/// <summary>
/// The joining peer's control request.
/// </summary>
/// <param name="Kind">Fresh join or reconnect.</param>
/// <param name="ContentHash">BIOS, disc, and tuning fingerprint.</param>
/// <param name="SessionId">Requested session identifier, or zero for a fresh host.</param>
/// <param name="LastFrame">Last frame the join successfully executed.</param>
/// <param name="LastFrameHash">Frame hash at <paramref name="LastFrame"/>.</param>
/// <param name="InputDelay">Requested input lead in emulated frames.</param>
/// <param name="PlayerCount">Connected player count.</param>
/// <param name="DiscId">Stable disc identity.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayJoinRequest(
    NetplayJoinKind Kind,
    ulong ContentHash,
    ulong SessionId,
    long LastFrame,
    uint LastFrameHash,
    byte InputDelay,
    byte PlayerCount,
    string DiscId);

/// <summary>
/// The hosting peer's control response.
/// </summary>
/// <param name="Accepted">Whether the request was accepted.</param>
/// <param name="Reason">Acceptance or failure reason.</param>
/// <param name="SessionId">Host session identifier.</param>
/// <param name="ContentHash">Host content fingerprint.</param>
/// <param name="StartFrame">First frame after the transferred state.</param>
/// <param name="InputDelay">Input lead the join must use.</param>
/// <param name="PlayerCount">Connected player count.</param>
/// <param name="Message">Human-readable detail.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayJoinResponse(
    bool Accepted,
    NetplayControlReason Reason,
    ulong SessionId,
    ulong ContentHash,
    long StartFrame,
    byte InputDelay,
    byte PlayerCount,
    string Message);

/// <summary>
/// A compressed machine state transferred over the control channel.
/// </summary>
public sealed class NetplayStateMessage
{
    /// <summary>Initializes a new state message.</summary>
    /// <param name="startFrame">Next frame after the state.</param>
    /// <param name="frameHash">Hash of the last assembled frame in the state.</param>
    /// <param name="data">Compressed save-state bytes.</param>
    public NetplayStateMessage(long startFrame, uint frameHash, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        StartFrame = startFrame;
        FrameHash = frameHash;
        Data = data;
    }

    /// <summary>Gets the next frame after the state.</summary>
    public long StartFrame { get; }

    /// <summary>Gets the hash of the last assembled frame in the state.</summary>
    public uint FrameHash { get; }

    /// <summary>Gets the compressed save-state bytes.</summary>
    public byte[] Data { get; }
}

/// <summary>
/// The joining peer's state acknowledgement.
/// </summary>
/// <param name="StartFrame">Frame that was loaded.</param>
/// <param name="FrameHash">Hash the join computed after loading.</param>
/// <param name="Ok">Whether the state and hash matched.</param>
/// <param name="Message">Human-readable detail.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayStateAck(
    long StartFrame,
    uint FrameHash,
    bool Ok,
    string Message);

/// <summary>
/// A control-channel close notification.
/// </summary>
/// <param name="Reason">Close reason.</param>
/// <param name="Message">Human-readable detail.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetplayCloseMessage(
    NetplayControlReason Reason,
    string Message);

/// <summary>
/// Encodes and validates reliable control-channel payloads.
/// </summary>
/// <remarks>
/// The channel framing is handled by <see cref="NetplayControlChannel"/>; this codec
/// writes a one-byte message type at the start of every payload and uses little-endian
/// scalar fields.
/// </remarks>
public static class NetplayControlCodec
{
    /// <summary>The largest accepted control payload.</summary>
    public const int MaxMessageSize = 8 * 1024 * 1024;

    /// <summary>Encodes a join request.</summary>
    /// <param name="request">Request to encode.</param>
    /// <returns>Payload bytes.</returns>
    public static byte[] Encode(in NetplayJoinRequest request)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)NetplayControlMessageType.JoinRequest);
        writer.Write((byte)request.Kind);
        writer.Write(request.ContentHash);
        writer.Write(request.SessionId);
        writer.Write(request.LastFrame);
        writer.Write(request.LastFrameHash);
        writer.Write(request.InputDelay);
        writer.Write(request.PlayerCount);
        writer.Write(request.DiscId ?? string.Empty);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes a join request.</summary>
    /// <param name="payload">Payload bytes.</param>
    /// <param name="request">Decoded request.</param>
    /// <returns><see langword="true"/> when the payload is a valid request.</returns>
    public static bool TryDecodeJoinRequest(
        ReadOnlySpan<byte> payload,
        out NetplayJoinRequest request)
    {
        request = default;
        try
        {
            using var reader = CreateReader(payload);
            if (reader.ReadByte() != (byte)NetplayControlMessageType.JoinRequest)
            {
                return false;
            }

            var kind = (NetplayJoinKind)reader.ReadByte();
            if (kind is not (NetplayJoinKind.Fresh or NetplayJoinKind.Reconnect))
            {
                return false;
            }

            var contentHash = reader.ReadUInt64();
            var sessionId = reader.ReadUInt64();
            var lastFrame = reader.ReadInt64();
            var lastFrameHash = reader.ReadUInt32();
            var inputDelay = reader.ReadByte();
            var playerCount = reader.ReadByte();
            var discId = reader.ReadString();
            if (!AtEnd(reader))
            {
                return false;
            }

            request = new NetplayJoinRequest(
                kind,
                contentHash,
                sessionId,
                lastFrame,
                lastFrameHash,
                inputDelay,
                playerCount,
                discId);
            return true;
        }
        catch (Exception exception) when (IsDecodeException(exception))
        {
            return false;
        }
    }

    /// <summary>Encodes a join response.</summary>
    /// <param name="response">Response to encode.</param>
    /// <returns>Payload bytes.</returns>
    public static byte[] Encode(in NetplayJoinResponse response)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)NetplayControlMessageType.JoinResponse);
        writer.Write(response.Accepted);
        writer.Write((byte)response.Reason);
        writer.Write(response.SessionId);
        writer.Write(response.ContentHash);
        writer.Write(response.StartFrame);
        writer.Write(response.InputDelay);
        writer.Write(response.PlayerCount);
        writer.Write(response.Message ?? string.Empty);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes a join response.</summary>
    /// <param name="payload">Payload bytes.</param>
    /// <param name="response">Decoded response.</param>
    /// <returns><see langword="true"/> when the payload is a valid response.</returns>
    public static bool TryDecodeJoinResponse(
        ReadOnlySpan<byte> payload,
        out NetplayJoinResponse response)
    {
        response = default;
        try
        {
            using var reader = CreateReader(payload);
            if (reader.ReadByte() != (byte)NetplayControlMessageType.JoinResponse)
            {
                return false;
            }

            var accepted = reader.ReadBoolean();
            var reason = (NetplayControlReason)reader.ReadByte();
            if (reason > NetplayControlReason.LocalStop)
            {
                return false;
            }

            var sessionId = reader.ReadUInt64();
            var contentHash = reader.ReadUInt64();
            var startFrame = reader.ReadInt64();
            var inputDelay = reader.ReadByte();
            var playerCount = reader.ReadByte();
            var message = reader.ReadString();
            if (!AtEnd(reader))
            {
                return false;
            }

            response = new NetplayJoinResponse(
                accepted,
                reason,
                sessionId,
                contentHash,
                startFrame,
                inputDelay,
                playerCount,
                message);
            return true;
        }
        catch (Exception exception) when (IsDecodeException(exception))
        {
            return false;
        }
    }

    /// <summary>Encodes a compressed state message.</summary>
    /// <param name="state">State to encode.</param>
    /// <returns>Payload bytes.</returns>
    public static byte[] Encode(NetplayStateMessage state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)NetplayControlMessageType.State);
        writer.Write(state.StartFrame);
        writer.Write(state.FrameHash);
        writer.Write(state.Data.Length);
        writer.Write(state.Data);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes a compressed state message.</summary>
    /// <param name="payload">Payload bytes.</param>
    /// <param name="state">Decoded state.</param>
    /// <returns><see langword="true"/> when the payload is a valid state message.</returns>
    public static bool TryDecodeState(ReadOnlySpan<byte> payload, out NetplayStateMessage? state)
    {
        state = null;
        try
        {
            using var reader = CreateReader(payload);
            if (reader.ReadByte() != (byte)NetplayControlMessageType.State)
            {
                return false;
            }

            var startFrame = reader.ReadInt64();
            var frameHash = reader.ReadUInt32();
            var length = reader.ReadInt32();
            if (length <= 0
                || length > MaxMessageSize
                || length != reader.BaseStream.Length - reader.BaseStream.Position)
            {
                return false;
            }

            var data = reader.ReadBytes(length);
            if (data.Length != length)
            {
                return false;
            }

            state = new NetplayStateMessage(startFrame, frameHash, data);
            return true;
        }
        catch (Exception exception) when (IsDecodeException(exception))
        {
            return false;
        }
    }

    /// <summary>Encodes a state acknowledgement.</summary>
    /// <param name="ack">Acknowledgement to encode.</param>
    /// <returns>Payload bytes.</returns>
    public static byte[] Encode(in NetplayStateAck ack)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)NetplayControlMessageType.StateAck);
        writer.Write(ack.StartFrame);
        writer.Write(ack.FrameHash);
        writer.Write(ack.Ok);
        writer.Write(ack.Message ?? string.Empty);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes a state acknowledgement.</summary>
    /// <param name="payload">Payload bytes.</param>
    /// <param name="ack">Decoded acknowledgement.</param>
    /// <returns><see langword="true"/> when the payload is a valid acknowledgement.</returns>
    public static bool TryDecodeStateAck(ReadOnlySpan<byte> payload, out NetplayStateAck ack)
    {
        ack = default;
        try
        {
            using var reader = CreateReader(payload);
            if (reader.ReadByte() != (byte)NetplayControlMessageType.StateAck)
            {
                return false;
            }

            var startFrame = reader.ReadInt64();
            var frameHash = reader.ReadUInt32();
            var ok = reader.ReadBoolean();
            var message = reader.ReadString();
            if (!AtEnd(reader))
            {
                return false;
            }

            ack = new NetplayStateAck(startFrame, frameHash, ok, message);
            return true;
        }
        catch (Exception exception) when (IsDecodeException(exception))
        {
            return false;
        }
    }

    /// <summary>Encodes a close notification.</summary>
    /// <param name="close">Close notification to encode.</param>
    /// <returns>Payload bytes.</returns>
    public static byte[] Encode(in NetplayCloseMessage close)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)NetplayControlMessageType.Close);
        writer.Write((byte)close.Reason);
        writer.Write(close.Message ?? string.Empty);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>Decodes a close notification.</summary>
    /// <param name="payload">Payload bytes.</param>
    /// <param name="close">Decoded close notification.</param>
    /// <returns><see langword="true"/> when the payload is a valid close notification.</returns>
    public static bool TryDecodeClose(ReadOnlySpan<byte> payload, out NetplayCloseMessage close)
    {
        close = default;
        try
        {
            using var reader = CreateReader(payload);
            if (reader.ReadByte() != (byte)NetplayControlMessageType.Close)
            {
                return false;
            }

            var reason = (NetplayControlReason)reader.ReadByte();
            if (reason > NetplayControlReason.LocalStop)
            {
                return false;
            }

            var message = reader.ReadString();
            if (!AtEnd(reader))
            {
                return false;
            }

            close = new NetplayCloseMessage(reason, message);
            return true;
        }
        catch (Exception exception) when (IsDecodeException(exception))
        {
            return false;
        }
    }

    private static BinaryReader CreateReader(ReadOnlySpan<byte> payload)
    {
        return new BinaryReader(new MemoryStream(payload.ToArray(), writable: false), Encoding.UTF8);
    }

    private static bool AtEnd(BinaryReader reader)
    {
        return reader.BaseStream.Position == reader.BaseStream.Length;
    }

    private static bool IsDecodeException(Exception exception)
    {
        return exception is EndOfStreamException
            or IOException
            or ArgumentException
            or FormatException
            or OverflowException;
    }
}
