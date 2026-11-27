using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FiveDo.Host.Netplay;

/// <summary>
/// A reliable TCP listener for netplay control messages and state transfer.
/// </summary>
/// <remarks>
/// The listener stays open across reconnects. Each accepted connection produces a
/// <see cref="NetplayControlChannel"/> framed with a four-byte magic, one protocol
/// version byte, one reserved byte, and a little-endian payload length.
/// </remarks>
public sealed class NetplayControlListener : IDisposable
{
    private readonly TcpListener _listener;
    private bool _disposed;

    /// <summary>Starts a listener on the requested IPv4 port.</summary>
    /// <param name="port">Port to bind, or zero for an ephemeral port.</param>
    /// <param name="bindAddress">Bind address. Defaults to all IPv4 interfaces.</param>
    public NetplayControlListener(int port, IPAddress? bindAddress = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);
        bindAddress ??= IPAddress.Any;
        _listener = new TcpListener(bindAddress, port);
        _listener.Start();
    }

    /// <summary>Gets the bound local endpoint.</summary>
    public IPEndPoint LocalEndPoint => (IPEndPoint)_listener.LocalEndpoint;

    /// <summary>Accepts one joining peer.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The accepted control channel.</returns>
    public async Task<NetplayControlChannel> AcceptAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        client.NoDelay = true;
        return new NetplayControlChannel(client);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listener.Stop();
    }
}

/// <summary>
/// A framed TCP connection used for netplay control and compressed state transfer.
/// </summary>
public sealed class NetplayControlChannel : IDisposable
{
    private const uint Magic = 0x434E4435; // "5DNC" in little-endian byte order.
    private const byte Version = 1;
    private const int HeaderSize = 10;
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private bool _disposed;

    internal NetplayControlChannel(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    /// <summary>Connects to a hosting peer.</summary>
    /// <param name="host">Host name or address.</param>
    /// <param name="port">Host control port.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connected control channel.</returns>
    public static async Task<NetplayControlChannel> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);
        var client = new TcpClient
        {
            NoDelay = true,
        };
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            return new NetplayControlChannel(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>Gets whether the underlying TCP connection is still usable.</summary>
    public bool Connected => !_disposed && _client.Connected;

    /// <summary>Sends one control payload.</summary>
    /// <param name="payload">Payload bytes, starting with a message-type byte.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the payload is written.</returns>
    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (payload.Length is <= 0 or > NetplayControlCodec.MaxMessageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                $"A control payload must be 1-{NetplayControlCodec.MaxMessageSize} bytes.");
        }

        var header = new byte[HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        header[4] = Version;
        header[5] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(6), payload.Length);
        await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Receives one control payload.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payload, or <see langword="null"/> on a clean end of stream.</returns>
    public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var header = new byte[HeaderSize];
        var read = await ReadExactAsync(header, allowCleanEnd: true, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return null;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic
            || header[4] != Version)
        {
            throw new InvalidDataException("The netplay control header is not recognized.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(6));
        if (length is <= 0 or > NetplayControlCodec.MaxMessageSize)
        {
            throw new InvalidDataException($"Invalid netplay control payload length {length}.");
        }

        var payload = new byte[length];
        _ = await ReadExactAsync(payload, allowCleanEnd: false, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
        _client.Dispose();
    }

    private async Task<int> ReadExactAsync(
        Memory<byte> destination,
        bool allowCleanEnd,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await _stream
                .ReadAsync(destination[total..], cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                if (total == 0 && allowCleanEnd)
                {
                    return 0;
                }

                throw new EndOfStreamException("The netplay control channel closed mid-message.");
            }

            total += read;
        }

        return total;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
