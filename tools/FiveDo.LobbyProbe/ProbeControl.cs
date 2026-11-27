using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;

namespace FiveDo.LobbyProbe;

internal sealed class ProbeControl : IDisposable
{
    private readonly TcpClient _client;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;

    private ProbeControl(TcpClient client)
    {
        _client = client;
        NetworkStream stream = client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n",
        };
    }

    public StreamReader Reader => _reader;

    public StreamWriter Writer => _writer;

    public IPEndPoint LocalEndPoint => (IPEndPoint)_client.Client.LocalEndPoint!;

    public static async Task<ProbeControl> ConnectAsync(
        RoomConnectionInfo room,
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
            await client.ConnectAsync(room.ControlRelay.Host, room.ControlRelay.Port, cancellationToken)
                .ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            var connect = new byte[LobbyRelayProtocol.ConnectSize];
            _ = LobbyRelayProtocol.EncodeConnect(
                connect,
                ParseRoomId(room.UdpRoomId),
                Guid.ParseExact(token, "N"),
                slot);
            await stream.WriteAsync(connect, cancellationToken).ConfigureAwait(false);
            var paired = new byte[LobbyRelayProtocol.PairedSize];
            await stream.ReadExactlyAsync(paired, cancellationToken).ConfigureAwait(false);
            if (!LobbyRelayProtocol.IsPairedPreamble(paired))
            {
                throw new InvalidDataException("The control relay preamble is invalid.");
            }

            return new ProbeControl(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _reader.Dispose();
        _writer.Dispose();
        _client.Dispose();
    }

    private static ulong ParseRoomId(string value)
    {
        return ulong.Parse(value, System.Globalization.NumberStyles.HexNumber);
    }
}
