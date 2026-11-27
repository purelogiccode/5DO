using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Host.Netplay.Lobby;

/// <summary>
/// Connects to a lobby TCP control relay and returns a byte-transparent netplay control
/// channel after the relay pairs the host and join.
/// </summary>
public static class LobbyControlRelay
{
    /// <summary>Connects to a room's TCP control relay.</summary>
    /// <param name="host">Lobby host name or IPv4 address.</param>
    /// <param name="port">TCP relay port.</param>
    /// <param name="roomId">Opaque room identifier.</param>
    /// <param name="token">Slot token.</param>
    /// <param name="slot">Player slot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paired netplay control channel.</returns>
    /// <exception cref="InvalidDataException">Thrown when the relay preamble is invalid.</exception>
    public static async Task<NetplayControlChannel> ConnectAsync(
        string host,
        int port,
        ulong roomId,
        Guid token,
        byte slot,
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
            NetworkStream stream = client.GetStream();
            var connect = new byte[LobbyRelayProtocol.ConnectSize];
            _ = LobbyRelayProtocol.EncodeConnect(connect, roomId, token, slot);
            await stream.WriteAsync(connect, cancellationToken).ConfigureAwait(false);
            var paired = new byte[LobbyRelayProtocol.PairedSize];
            await stream.ReadExactlyAsync(paired, cancellationToken).ConfigureAwait(false);
            if (!LobbyRelayProtocol.IsPairedPreamble(paired))
            {
                throw new InvalidDataException("The lobby control relay preamble is not recognized.");
            }

            return new NetplayControlChannel(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
