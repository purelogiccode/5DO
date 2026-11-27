using System;
using System.Net;
using System.Net.Sockets;

namespace FiveDo.Host.Netplay;

/// <summary>
/// A datagram transport used by a lockstep session.
/// </summary>
public interface INetplayTransport : IDisposable
{
    /// <summary>Gets a value indicating whether a remote endpoint is known.</summary>
    bool HasRemote { get; }

    /// <summary>Sends one datagram to the remote endpoint.</summary>
    /// <param name="packet">Complete protocol packet.</param>
    void Send(ReadOnlySpan<byte> packet);

    /// <summary>Receives one datagram when one is already queued.</summary>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="length">Received payload length.</param>
    /// <returns><see langword="true"/> when a datagram was received.</returns>
    bool TryReceive(Span<byte> destination, out int length);
}

/// <summary>
/// A UDP transport for direct-IP netplay. The hosting peer learns the joining peer's
/// endpoint from the first handshake datagram; the joining peer starts with it.
/// </summary>
public sealed class UdpNetplayTransport : INetplayTransport
{
    private readonly Socket _socket;
    private IPEndPoint? _remote;

    /// <summary>Initializes a new UDP transport.</summary>
    /// <param name="localPort">Local port to bind. Zero selects an ephemeral port.</param>
    /// <param name="remote">
    /// Remote endpoint for a joining peer, or <see langword="null"/> for a hosting peer.
    /// </param>
    /// <param name="bindAddress">
    /// Address to bind. Defaults to the IPv4 loopback address.
    /// </param>
    public UdpNetplayTransport(int localPort, IPEndPoint? remote = null, IPAddress? bindAddress = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(localPort);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(localPort, ushort.MaxValue);
        bindAddress ??= IPAddress.Loopback;
        _remote = remote;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(bindAddress, localPort));
        _socket.ReceiveBufferSize = 64 * 1024;
        _socket.SendBufferSize = 64 * 1024;
    }

    /// <summary>Gets the local endpoint the transport is bound to.</summary>
    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    /// <summary>Gets the remote endpoint, when one has been learned.</summary>
    public IPEndPoint? RemoteEndPoint => _remote;

    /// <inheritdoc />
    public bool HasRemote => _remote is not null;

    /// <inheritdoc />
    public void Send(ReadOnlySpan<byte> packet)
    {
        if (_remote is null)
        {
            throw new InvalidOperationException("The remote netplay endpoint is not known yet.");
        }

        _socket.SendTo(packet, _remote);
    }

    /// <inheritdoc />
    public bool TryReceive(Span<byte> destination, out int length)
    {
        length = 0;
        if (!_socket.Poll(0, SelectMode.SelectRead))
        {
            return false;
        }

        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        try
        {
            length = _socket.ReceiveFrom(destination, ref from);
        }
        catch (SocketException)
        {
            return false;
        }

        if (_remote is null && from is IPEndPoint endpoint)
        {
            _remote = endpoint;
        }

        return length > 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _socket.Dispose();
    }
}
