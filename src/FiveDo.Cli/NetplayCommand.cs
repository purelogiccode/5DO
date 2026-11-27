using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Host.Netplay;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;

namespace FiveDo.Cli;

internal static class NetplayCommand
{
    public static int Run(string[] args)
    {
        return RunAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(string[] args)
    {
        NetplayCliOptions options = NetplayCliOptions.Parse(args);
        byte[] bios1 = File.ReadAllBytes(options.BiosPath);
        byte[] bios2 = options.Bios2Path is null ? [] : File.ReadAllBytes(options.Bios2Path);
        EmulationProfile profile = options.Profile switch
        {
            "accuracy" => EmulationProfile.Accuracy,
            "performance" => EmulationProfile.Performance,
            _ => EmulationProfile.Compatible,
        };
        EmulatorTuning tuning = EmulationProfiles.Apply(profile) with
        {
            HighResolution = options.HighResolution || options.TrueHighResolution,
            TrueHighResolution = options.TrueHighResolution,
        };
        string discId;
        using (IDiscSource disc = DiscImageFactory.Open(options.DiscPath))
        {
            discId = GameId.Compute(disc);
        }

        ulong contentHash = NetplayContentHash.Compute(bios1, bios2, discId, tuning);
        Func<long, ControllerState> localInput = LoadInputScript(options.InputScriptPath);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        NetplayRunResult result = string.Equals(options.Mode, "host", StringComparison.Ordinal)
            ? await RunHostAsync(options, bios1, bios2, tuning, discId, contentHash, localInput)
                .ConfigureAwait(false)
            : await RunJoinAsync(options, bios1, bios2, tuning, discId, contentHash, localInput)
                .ConfigureAwait(false);
        clock.Stop();

        var report = new CliNetplayResult(
            result.Ok,
            options.Mode,
            options.Host ?? string.Empty,
            result.Port,
            result.Frames,
            result.StartFrame,
            FormattableString.Invariant($"0x{result.SessionId:X16}"),
            FormattableString.Invariant($"0x{contentHash:X16}"),
            result.FinalFrameHash,
            result.FinalAudioHash,
            result.Reconnects,
            result.BytesSent,
            result.BytesReceived,
            clock.Elapsed.TotalSeconds,
            result.Failure);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, CliJsonContext.Default.CliNetplayResult));
        }
        else
        {
            Console.WriteLine($"role:       {options.Mode}");
            if (string.Equals(options.Mode, "join", StringComparison.Ordinal))
            {
                Console.WriteLine($"host:       {options.Host}:{result.Port}");
            }
            else
            {
                Console.WriteLine($"port:       {result.Port}");
            }

            Console.WriteLine($"frames:     {result.Frames}");
            Console.WriteLine($"start:      {result.StartFrame}");
            Console.WriteLine($"session:    {report.SessionId}");
            Console.WriteLine($"hash:       0x{result.FinalFrameHash:X8}");
            Console.WriteLine($"audio:      0x{result.FinalAudioHash:X8}");
            Console.WriteLine($"reconnects: {result.Reconnects}");
            Console.WriteLine($"traffic:    {result.BytesSent + result.BytesReceived:N0} bytes");
            if (result.Failure is not null)
            {
                Console.WriteLine($"failure:    {result.Failure}");
            }

            Console.WriteLine(result.Ok ? "result:     PASS" : "result:     FAIL");
        }

        return result.Ok ? 0 : 1;
    }

    private static async Task<NetplayRunResult> RunHostAsync(
        NetplayCliOptions options,
        byte[] bios1,
        byte[] bios2,
        EmulatorTuning tuning,
        string discId,
        ulong contentHash,
        Func<long, ControllerState> localInput)
    {
        using var listener = new NetplayControlListener(options.Port);
        int port = listener.LocalEndPoint.Port;
        using var stop = new CancellationTokenSource();
        using IDiscSource disc = DiscImageFactory.Open(options.DiscPath);
        FiveDoMachine machine = CreateMachine(bios1, bios2, disc, tuning);
        ulong sessionId = CreateSessionId();
        var totalFrames = 0L;
        var reconnects = 0;
        var bytesSent = 0L;
        var bytesReceived = 0L;
        var lastFrame = 0L;
        var initialStartFrame = -1L;
        var lastHash = 0u;
        var lastAudioHash = 0u;
        string? failure = null;
        Console.Error.WriteLine($"5DO netplay host listening on TCP/UDP port {port}.");

        while (totalFrames < options.Frames)
        {
            NetplayControlChannel control;
            try
            {
                using var accept = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                accept.CancelAfter(TimeSpan.FromSeconds(options.ReconnectTimeoutSeconds));
                control = await listener.AcceptAsync(accept.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                failure = "Timed out waiting for a joining peer.";
                break;
            }

            using (control)
            {
                byte[]? payload = await control.ReceiveAsync(stop.Token).ConfigureAwait(false);
                if (payload is null
                    || !NetplayControlCodec.TryDecodeJoinRequest(payload, out NetplayJoinRequest request))
                {
                    failure = "The joining peer sent an invalid control request.";
                    break;
                }

                if (request.ContentHash != contentHash
                    || request.PlayerCount != 2
                    || !string.Equals(request.DiscId, discId, StringComparison.Ordinal))
                {
                    await SendResponseAsync(
                            control,
                            new NetplayJoinResponse(
                                false,
                                NetplayControlReason.ContentMismatch,
                                sessionId,
                                contentHash,
                                0,
                                options.InputDelay,
                                2,
                                "BIOS, disc, tuning, or player count does not match."),
                            stop.Token)
                        .ConfigureAwait(false);
                    failure = "The joining peer does not match this BIOS, disc, and tuning.";
                    break;
                }

                NetplayStateMessage state = NetplayStateTransfer.Capture(machine, discId);
                if (initialStartFrame < 0)
                {
                    initialStartFrame = state.StartFrame;
                }

                lastFrame = state.StartFrame;
                failure = null;
                lastHash = state.FrameHash;
                var response = new NetplayJoinResponse(
                    true,
                    NetplayControlReason.Accepted,
                    sessionId,
                    contentHash,
                    state.StartFrame,
                    options.InputDelay,
                    2,
                    string.Empty);
                await SendResponseAsync(control, response, stop.Token).ConfigureAwait(false);
                bytesSent += NetplayControlCodec.Encode(response).Length;
                byte[] statePayload = NetplayControlCodec.Encode(state);
                await control.SendAsync(statePayload, stop.Token).ConfigureAwait(false);
                bytesSent += statePayload.Length;

                byte[]? ackPayload = await control.ReceiveAsync(stop.Token).ConfigureAwait(false);
                bytesReceived += ackPayload?.Length ?? 0;
                if (ackPayload is null
                    || !NetplayControlCodec.TryDecodeStateAck(ackPayload, out NetplayStateAck ack)
                    || !ack.Ok
                    || ack.StartFrame != state.StartFrame
                    || ack.FrameHash != state.FrameHash)
                {
                    failure = ackPayload is null
                        ? "The joining peer closed the control channel during state sync."
                        : "The joining peer rejected or mis-validated the transferred state.";
                    break;
                }

                Console.Error.WriteLine(
                    $"5DO netplay host: player 2 connected; state at frame {state.StartFrame}, "
                    + $"running {options.Frames - totalFrames} frame(s).");
            }

            using var udp = new UdpNetplayTransport(port, remote: null, bindAddress: IPAddress.Any);
            using var session = new LockstepSession(
                udp,
                new NetplayOptions
                {
                    Role = NetplayRole.Host,
                    InputDelayFrames = options.InputDelay,
                    SessionId = sessionId,
                    ContentHash = contentHash,
                    FirstFrame = lastFrame,
                    CompareAudioHashes = !options.NoAudioHash,
                    StepTimeout = TimeSpan.FromSeconds(5),
                },
                localInput);
            if (!session.StartNegotiated())
            {
                failure = session.FailureMessage ?? "The hosting session did not start.";
                break;
            }

            using var peer = new NetplayMachinePeer(machine, session);
            long remaining = options.Frames - totalFrames;
            var run = RunPeer(peer, remaining);
            totalFrames += run.Frames;
            lastFrame = run.LastFrame >= 0 ? run.LastFrame : machine.FramesExecuted;
            lastHash = run.LastFrameHash;
            lastAudioHash = run.LastAudioHash;
            bytesSent += session.Metrics.BytesSent;
            bytesReceived += session.Metrics.BytesReceived;
            if (run.Completed)
            {
                break;
            }

            failure = run.Failure;
            if (run.SessionState == NetplaySessionState.Desynchronized
                || options.ReconnectTimeoutSeconds == 0)
            {
                break;
            }

            reconnects++;
            Console.Error.WriteLine(
                $"5DO netplay host: connection lost at frame {lastFrame}; waiting for reconnect.");
        }

        return new NetplayRunResult(
            failure is null && totalFrames == options.Frames,
            port,
            totalFrames,
            initialStartFrame >= 0 ? initialStartFrame : lastFrame,
            sessionId,
            lastHash,
            lastAudioHash,
            reconnects,
            bytesSent,
            bytesReceived,
            failure);
    }

    private static async Task<NetplayRunResult> RunJoinAsync(
        NetplayCliOptions options,
        byte[] bios1,
        byte[] bios2,
        EmulatorTuning tuning,
        string discId,
        ulong contentHash,
        Func<long, ControllerState> localInput)
    {
        string host = options.Host!;
        IPAddress address = ResolveAddress(host);
        using IDiscSource disc = DiscImageFactory.Open(options.DiscPath);
        FiveDoMachine machine = CreateMachine(bios1, bios2, disc, tuning);
        var totalFrames = 0L;
        var reconnects = 0;
        var bytesSent = 0L;
        var bytesReceived = 0L;
        var lastFrame = 0L;
        var initialStartFrame = -1L;
        var lastHash = 0u;
        var lastAudioHash = 0u;
        ulong sessionId = 0;
        string? failure = null;
        Console.Error.WriteLine($"5DO netplay join connecting to {host}:{options.Port}.");

        while (totalFrames < options.Frames)
        {
            NetplayControlChannel control;
            try
            {
                using var connect = new CancellationTokenSource(
                    TimeSpan.FromSeconds(options.ReconnectTimeoutSeconds));
                control = await NetplayControlChannel
                    .ConnectAsync(address.ToString(), options.Port, connect.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                failure = "Timed out connecting to the hosting peer.";
                break;
            }
            catch (SocketException exception)
            {
                failure = $"Could not connect to the hosting peer: {exception.Message}";
                break;
            }

            using (control)
            {
                var request = new NetplayJoinRequest(
                    reconnects == 0 ? NetplayJoinKind.Fresh : NetplayJoinKind.Reconnect,
                    contentHash,
                    sessionId,
                    lastFrame,
                    lastHash,
                    options.InputDelay,
                    2,
                    discId);
                byte[] requestPayload = NetplayControlCodec.Encode(request);
                await control.SendAsync(requestPayload, CancellationToken.None).ConfigureAwait(false);
                bytesSent += requestPayload.Length;

                byte[]? responsePayload = await control.ReceiveAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                bytesReceived += responsePayload?.Length ?? 0;
                if (responsePayload is null
                    || !NetplayControlCodec.TryDecodeJoinResponse(responsePayload, out NetplayJoinResponse response)
                    || !response.Accepted)
                {
                    failure = responsePayload is null
                        ? "The hosting peer closed the control channel."
                        : "The hosting peer rejected the join request.";
                    break;
                }

                if (response.ContentHash != contentHash)
                {
                    failure = "The hosting peer's BIOS, disc, or tuning does not match.";
                    break;
                }

                sessionId = response.SessionId;
                byte[]? statePayload = await control.ReceiveAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                bytesReceived += statePayload?.Length ?? 0;
                if (statePayload is null
                    || !NetplayControlCodec.TryDecodeState(statePayload, out NetplayStateMessage? state)
                    || state is null)
                {
                    failure = "The hosting peer did not send a readable state.";
                    break;
                }

                if (!NetplayStateTransfer.TryApply(machine, state, discId, out string? stateError))
                {
                    var nack = new NetplayStateAck(state.StartFrame, state.FrameHash, false, stateError ?? "invalid");
                    await control.SendAsync(NetplayControlCodec.Encode(nack), CancellationToken.None)
                        .ConfigureAwait(false);
                    failure = stateError;
                    break;
                }

                var ack = new NetplayStateAck(state.StartFrame, state.FrameHash, true, string.Empty);
                await control.SendAsync(NetplayControlCodec.Encode(ack), CancellationToken.None)
                    .ConfigureAwait(false);
                bytesSent += NetplayControlCodec.Encode(ack).Length;
                if (initialStartFrame < 0)
                {
                    initialStartFrame = state.StartFrame;
                }

                lastFrame = state.StartFrame;
                lastHash = state.FrameHash;
                failure = null;
                Console.Error.WriteLine(
                    $"5DO netplay join: state loaded at frame {state.StartFrame}; "
                    + $"running {options.Frames - totalFrames} frame(s).");
            }

            using var udp = new UdpNetplayTransport(
                0,
                new IPEndPoint(address, options.Port),
                IPAddress.Any);
            using var session = new LockstepSession(
                udp,
                new NetplayOptions
                {
                    Role = NetplayRole.Join,
                    InputDelayFrames = options.InputDelay,
                    SessionId = sessionId,
                    ContentHash = contentHash,
                    FirstFrame = lastFrame,
                    CompareAudioHashes = !options.NoAudioHash,
                    StepTimeout = TimeSpan.FromSeconds(5),
                },
                localInput);
            if (!session.StartNegotiated())
            {
                failure = session.FailureMessage ?? "The joining session did not start.";
                break;
            }

            using var peer = new NetplayMachinePeer(machine, session);
            long remaining = options.Frames - totalFrames;
            var simulatedExit = options.ExitAfterFrames > 0
                && reconnects == 0
                && remaining > options.ExitAfterFrames;
            if (simulatedExit)
            {
                remaining = options.ExitAfterFrames;
            }

            var run = RunPeer(peer, remaining, complete: !simulatedExit);
            totalFrames += run.Frames;
            lastFrame = run.LastFrame >= 0 ? run.LastFrame : machine.FramesExecuted;
            lastHash = run.LastFrameHash;
            lastAudioHash = run.LastAudioHash;
            bytesSent += session.Metrics.BytesSent;
            bytesReceived += session.Metrics.BytesReceived;
            if (simulatedExit)
            {
                failure = "Simulated join exit for reconnect testing.";
                break;
            }

            if (run.Completed)
            {
                break;
            }

            failure = run.Failure;
            if (run.SessionState == NetplaySessionState.Desynchronized
                || options.ReconnectTimeoutSeconds == 0)
            {
                break;
            }

            reconnects++;
        }

        return new NetplayRunResult(
            failure is null && totalFrames == options.Frames,
            options.Port,
            totalFrames,
            initialStartFrame >= 0 ? initialStartFrame : lastFrame,
            sessionId,
            lastHash,
            lastAudioHash,
            reconnects,
            bytesSent,
            bytesReceived,
            failure);
    }

    private static async Task SendResponseAsync(
        NetplayControlChannel control,
        NetplayJoinResponse response,
        CancellationToken cancellationToken)
    {
        await control.SendAsync(NetplayControlCodec.Encode(response), cancellationToken)
            .ConfigureAwait(false);
    }

    private static FiveDoMachine CreateMachine(
        byte[] bios1,
        byte[] bios2,
        IDiscSource disc,
        EmulatorTuning tuning)
    {
        var machine = new FiveDoMachine(tuning);
        machine.LoadBios(bios1, bios2);
        machine.InsertDisc(disc);
        machine.Reset();
        machine.LoadNvram(NvramImage.CreateFresh().Data.Span);
        return machine;
    }

    private static Func<long, ControllerState> LoadInputScript(string? path)
    {
        if (path is null)
        {
            return _ => ControllerState.Empty;
        }

        IReadOnlyList<InputScriptEntry> entries = InputScript.Load(path);
        return frame => FindEntry(entries, frame);
    }

    private static ControllerState FindEntry(IReadOnlyList<InputScriptEntry> entries, long frame)
    {
        var low = 0;
        var high = entries.Count - 1;
        var match = -1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (entries[middle].Frame <= frame)
            {
                match = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return match >= 0 ? entries[match].State : ControllerState.Empty;
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            return address;
        }

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        return addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);
    }

    private static ulong CreateSessionId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static PeerRun RunPeer(NetplayMachinePeer peer, long frames, bool complete = true)
    {
        var count = 0L;
        var completed = false;
        for (var i = 0L; i < frames; i++)
        {
            NetplayFrameResult result = peer.RunFrame();
            if (!result.Advanced)
            {
                break;
            }

            count++;
        }

        if (complete && peer.Session.State == NetplaySessionState.Running)
        {
            peer.Session.Complete();
            completed = true;
        }

        return new PeerRun(
            count,
            peer.LastPublishedFrame,
            peer.LastPublishedFrameHash,
            peer.Audio.Hash,
            completed,
            peer.Session.State,
            peer.Session.FailureMessage);
    }

    private readonly record struct PeerRun(
        long Frames,
        long LastFrame,
        uint LastFrameHash,
        uint LastAudioHash,
        bool Completed,
        NetplaySessionState SessionState,
        string? Failure);

    private readonly record struct NetplayRunResult(
        bool Ok,
        int Port,
        long Frames,
        long StartFrame,
        ulong SessionId,
        uint FinalFrameHash,
        uint FinalAudioHash,
        int Reconnects,
        long BytesSent,
        long BytesReceived,
        string? Failure);

    private sealed class NetplayCliOptions
    {
        public required string Mode { get; init; }

        public string? Host { get; init; }

        public required string DiscPath { get; init; }

        public required string BiosPath { get; init; }

        public string? Bios2Path { get; init; }

        public string? InputScriptPath { get; init; }

        public int Port { get; init; } = 5600;

        public byte InputDelay { get; init; } = 4;

        public int Frames { get; init; } = 3600;

        public int ReconnectTimeoutSeconds { get; init; } = 30;

        public int ExitAfterFrames { get; init; }

        public string Profile { get; init; } = "compatible";

        public bool Json { get; init; }

        public bool HighResolution { get; init; }

        public bool TrueHighResolution { get; init; }

        public bool NoAudioHash { get; init; }

        public static NetplayCliOptions Parse(string[] args)
        {
            if (args.Length < 2
                || (!string.Equals(args[1], "host", StringComparison.Ordinal)
                    && !string.Equals(args[1], "join", StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Use 'netplay host <disc>' or 'netplay join <host> <disc>'.",
                    nameof(args));
            }

            var mode = args[1];
            var positionals = new List<string>();
            string? bios = null;
            string? bios2 = null;
            string? inputScript = null;
            var port = 5600;
            var delay = 4;
            var frames = 3600;
            var reconnect = 30;
            var exitAfter = 0;
            var profile = "compatible";
            var json = false;
            var highResolution = false;
            var trueHighResolution = false;
            var noAudioHash = false;

            for (var i = 2; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--bios":
                        bios = Next(args, ref i);
                        break;
                    case "--bios2":
                        bios2 = Next(args, ref i);
                        break;
                    case "--input-script":
                        inputScript = Next(args, ref i);
                        break;
                    case "--port":
                        port = ParseInt(Next(args, ref i), "--port");
                        break;
                    case "--delay":
                        delay = ParseInt(Next(args, ref i), "--delay");
                        break;
                    case "--frames":
                        frames = ParseInt(Next(args, ref i), "--frames");
                        break;
                    case "--reconnect-timeout":
                        reconnect = ParseInt(Next(args, ref i), "--reconnect-timeout");
                        break;
                    case "--exit-after":
                        exitAfter = ParseInt(Next(args, ref i), "--exit-after");
                        break;
                    case "--profile":
                        profile = Next(args, ref i);
                        break;
                    case "--json":
                        json = true;
                        break;
                    case "--high-res":
                        highResolution = true;
                        break;
                    case "--true-2x":
                        trueHighResolution = true;
                        highResolution = true;
                        break;
                    case "--no-audio-hash":
                        noAudioHash = true;
                        break;
                    default:
                        if (args[i].StartsWith("--", StringComparison.Ordinal))
                        {
                            throw new ArgumentException($"Unknown netplay option '{args[i]}'.", nameof(args));
                        }

                        positionals.Add(args[i]);
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(bios))
            {
                throw new ArgumentException("Netplay requires --bios <path>.", nameof(args));
            }

            if (positionals.Count == 0)
            {
                throw new ArgumentException(
                    string.Equals(mode, "host", StringComparison.Ordinal)
                        ? "Netplay host requires a disc path."
                        : "Netplay join requires a host and a disc path.",
                    nameof(args));
            }

            string? host = null;
            string disc;
            if (string.Equals(mode, "host", StringComparison.Ordinal))
            {
                if (positionals.Count != 1)
                {
                    throw new ArgumentException("Netplay host takes exactly one disc path.", nameof(args));
                }

                disc = positionals[0];
            }
            else
            {
                if (positionals.Count != 2)
                {
                    throw new ArgumentException("Netplay join takes a host and a disc path.", nameof(args));
                }

                host = positionals[0];
                disc = positionals[1];
            }

            if (port is < 1 or > 65535)
            {
                throw new ArgumentException("--port must be between 1 and 65535.", nameof(args));
            }

            if (delay is < 0 or > 120)
            {
                throw new ArgumentException("--delay must be between 0 and 120.", nameof(args));
            }

            if (frames <= 0)
            {
                throw new ArgumentException("--frames must be positive.", nameof(args));
            }

            if (reconnect < 0 || exitAfter < 0)
            {
                throw new ArgumentException(
                    "--reconnect-timeout and --exit-after must be zero or positive.",
                    nameof(args));
            }

            if (profile is not ("compatible" or "accuracy" or "performance"))
            {
                throw new ArgumentException("--profile must be compatible, accuracy, or performance.", nameof(args));
            }

            return new NetplayCliOptions
            {
                Mode = mode,
                Host = host,
                DiscPath = disc,
                BiosPath = bios,
                Bios2Path = bios2,
                InputScriptPath = inputScript,
                Port = port,
                InputDelay = (byte)delay,
                Frames = frames,
                ReconnectTimeoutSeconds = reconnect,
                ExitAfterFrames = exitAfter,
                Profile = profile,
                Json = json,
                HighResolution = highResolution,
                TrueHighResolution = trueHighResolution,
                NoAudioHash = noAudioHash,
            };
        }

        private static string Next(string[] args, ref int index)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value after '{args[index]}'.", nameof(args));
            }

            index++;
            return args[index];
        }

        private static int ParseInt(string value, string option)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                throw new ArgumentException($"{option} requires an integer, got '{value}'.", nameof(value));
            }

            return result;
        }
    }
}
