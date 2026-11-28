using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Lobby.Protocol;

namespace FiveDo.LobbyProbe;

internal static class Program
{
    [SuppressMessage(
        "ReSharper",
        "AccessToDisposedClosure",
        Justification = "The handler is unsubscribed before the cancellation source is disposed.")]
    public static async Task<int> Main(string[] args)
    {
        ProbeOptions options;
        try
        {
            options = ProbeOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(ProbeOptions.Usage);
            return 2;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            return await RunAsync(options, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Probe canceled.");
            return 130;
        }
        catch (Exception exception) when (exception is LobbyApiException
            or HttpRequestException
            or SocketException
            or IOException
            or InvalidDataException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static async Task<int> RunAsync(ProbeOptions options, CancellationToken cancellationToken)
    {
        using var api = new LobbyApiClient(options.Lobby);
        RoomConnectionInfo room;
        string token;
        byte slot;
        if (options.Host)
        {
            CreateRoomResponse created = await api
                .CreateRoomAsync(
                    new CreateRoomRequest
                    {
                        Emulator = options.Emulator,
                        ProtocolVersion = LobbyProtocol.Version,
                        ContentId = options.Content,
                        ContentHash = options.Hash,
                        DisplayName = options.Name,
                        MaxPlayers = 2,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            room = created.Room;
            token = created.HostToken;
            slot = 0;
            Console.WriteLine(
                $"host room {room.JoinCode} udp={room.UdpRoomId} relay={room.ControlRelay.Port}/{room.UdpRelay.Port}");
            Console.WriteLine(
                $"join with: FiveDo.LobbyProbe join --lobby {options.Lobby} --code {room.JoinCode}");
        }
        else
        {
            JoinRoomResponse joined = await api
                .JoinRoomAsync(
                    options.Code!,
                    new JoinRoomRequest
                    {
                        Emulator = options.Emulator,
                        ProtocolVersion = LobbyProtocol.Version,
                        ContentId = options.Content,
                        ContentHash = options.Hash,
                        DisplayName = options.Name,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            room = joined.Room;
            token = joined.SlotToken;
            slot = (byte)joined.Slot;
            Console.WriteLine(
                $"join room {room.JoinCode} slot={slot} relay={room.ControlRelay.Port}/{room.UdpRelay.Port}");
        }

        using var udp = new ProbeUdpPeer(room, token, slot, options.Mode, options.LocalPort);
        using var signal = options.UseSignal
            ? await ProbeSignal.ConnectAsync(options.Lobby, room, token, cancellationToken).ConfigureAwait(false)
            : null;
        using var control = await ProbeControl.ConnectAsync(room, token, slot, cancellationToken)
            .ConfigureAwait(false);
        await ExchangeControlAsync(control, options.Host, room.JoinCode, cancellationToken)
            .ConfigureAwait(false);
        Console.WriteLine($"control relay: paired ({control.LocalEndPoint})");
        if (signal is not null)
        {
            await signal.SendTransportAsync(options.Mode, cancellationToken).ConfigureAwait(false);
        }

        var sent = 0;
        var received = 0;
        var pongs = 0;
        double lastRtt = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var iteration = 0; iteration < options.Pings; iteration++)
        {
            foreach (byte[] payload in udp.Poll())
            {
                received++;
                string text = Encoding.UTF8.GetString(payload);
                if (options.Host && text.StartsWith("PONG ", StringComparison.Ordinal))
                {
                    pongs++;
                    string[] parts = text.Split(' ');
                    if (parts.Length >= 3
                        && long.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                    {
                        lastRtt = clock.Elapsed.TotalMilliseconds - (ticks / (double)System.Diagnostics.Stopwatch.Frequency * 1000);
                    }

                    Console.WriteLine($"udp: {text} rtt={lastRtt:F1} ms path={udp.TransportMode}");
                }
                else if (!options.Host && text.StartsWith("PING ", StringComparison.Ordinal))
                {
                    string[] parts = text.Split(' ');
                    byte[] reply = Encoding.UTF8.GetBytes($"PONG {parts[1]} {parts[2]}");
                    udp.Send(reply);
                    sent++;
                }
                else
                {
                    Console.WriteLine($"udp: {text} path={udp.TransportMode}");
                }
            }

            if (options.Host)
            {
                byte[] ping = Encoding.UTF8.GetBytes(
                    $"PING {iteration} {clock.ElapsedTicks.ToString(CultureInfo.InvariantCulture)}");
                udp.Send(ping);
                sent++;
            }

            await Task.Delay(options.IntervalMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        foreach (byte[] payload in udp.Poll())
        {
            received++;
            string text = Encoding.UTF8.GetString(payload);
            if (options.Host && text.StartsWith("PONG ", StringComparison.Ordinal))
            {
                pongs++;
            }

            Console.WriteLine($"udp: {text}");
        }

        Console.WriteLine(
            $"summary: emulator={options.Emulator} room={room.JoinCode} slot={slot} path={udp.TransportMode} "
            + $"local={udp.LocalEndPoint} peer={udp.PeerEndPoint} sent={sent} received={received} pongs={pongs}");
        if (options.Host)
        {
            await api.CloseRoomAsync(room.JoinCode, token, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine("room closed.");
        }

        return options.Host ? (pongs > 0 || received > 0 ? 0 : 1) : 0;
    }

    private static async Task ExchangeControlAsync(
        ProbeControl control,
        bool host,
        string room,
        CancellationToken cancellationToken)
    {
        if (host)
        {
            await control.Writer.WriteLineAsync($"HOST {room}").ConfigureAwait(false);
            string? join = await control.Reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(join, $"JOIN {room}", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected control reply '{join}'.");
            }

            await control.Writer.WriteLineAsync($"ACK {room}").ConfigureAwait(false);
            string? done = await control.Reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(done, $"DONE {room}", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected control reply '{done}'.");
            }
        }
        else
        {
            string? hello = await control.Reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(hello, $"HOST {room}", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected control hello '{hello}'.");
            }

            await control.Writer.WriteLineAsync($"JOIN {room}").ConfigureAwait(false);
            string? ack = await control.Reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(ack, $"ACK {room}", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected control reply '{ack}'.");
            }

            await control.Writer.WriteLineAsync($"DONE {room}").ConfigureAwait(false);
        }
    }
}
