using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Host.Netplay;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.NetplayProbe;

internal static class ProbeRunner
{
    public static ProbeReport Run(ProbeOptions options)
    {
        var report = new ProbeReport
        {
            Disc = Path.GetFileName(options.DiscPath),
            Bios = Path.GetFileName(options.BiosPath),
            Profile = options.Profile,
            Frames = options.Frames,
            InputDelay = options.InputDelay,
            LatencyMilliseconds = options.LatencyMilliseconds,
            DropInputEvery = options.DropInputEvery,
        };

        byte[] bios1 = File.ReadAllBytes(options.BiosPath);
        byte[] bios2 = options.Bios2Path is null ? [] : File.ReadAllBytes(options.Bios2Path);
        EmulationProfile profile = options.Profile switch
        {
            "accuracy" => EmulationProfile.Accuracy,
            "performance" => EmulationProfile.Performance,
            _ => EmulationProfile.Compatible,
        };
        EmulatorTuning tuning = EmulationProfiles.Apply(profile);

        string discId;
        using (IDiscSource identity = DiscImageFactory.Open(options.DiscPath))
        {
            discId = GameId.Compute(identity);
        }

        ulong contentHash = NetplayContentHash.Compute(bios1, bios2, discId, tuning);
        report.ContentHash = FormattableString.Invariant($"0x{contentHash:X16}");

        Func<long, ControllerState> p1 = ProbeInput.Load(options.P1ScriptPath, DefaultPlayer1);
        Func<long, ControllerState> p2 = ProbeInput.Load(options.P2ScriptPath, DefaultPlayer2);

        using var hostTransport = BuildTransport(options, host: true);
        using var joinTransport = BuildTransport(options, host: false);
        var netplayOptions = new NetplayOptions
        {
            InputDelayFrames = options.InputDelay,
            ContentHash = contentHash,
            StepTimeout = TimeSpan.FromSeconds(10),
        };
        using var hostSession = new LockstepSession(
            hostTransport,
            netplayOptions with { Role = NetplayRole.Host },
            p1);
        using var joinSession = new LockstepSession(
            joinTransport,
            netplayOptions with { Role = NetplayRole.Join },
            p2);

        using IDiscSource hostDisc = DiscImageFactory.Open(options.DiscPath);
        using IDiscSource joinDisc = DiscImageFactory.Open(options.DiscPath);
        FiveDoMachine hostMachine = CreateMachine(bios1, bios2, hostDisc, tuning);
        FiveDoMachine joinMachine = CreateMachine(bios1, bios2, joinDisc, tuning);
        using var hostPeer = new NetplayMachinePeer(hostMachine, hostSession);
        using var joinPeer = new NetplayMachinePeer(joinMachine, joinSession);
        var checkpointInterval = Math.Max(1, options.Frames / 30);

        var clock = Stopwatch.StartNew();
        PeerRunReport[] peers = RunPair(hostPeer, joinPeer, options.Frames, checkpointInterval);
        clock.Stop();

        report.Host = peers[0];
        report.Join = peers[1];
        report.WallSeconds = clock.Elapsed.TotalSeconds;
        report.FramesPerSecond = report.WallSeconds > 0 ? options.Frames / report.WallSeconds : 0;

        if (options.RunReference)
        {
            report.Reference = RunReference(
                options,
                bios1,
                bios2,
                tuning,
                p1,
                p2,
                checkpointInterval);
        }

        if (options.MeasureStateCost)
        {
            report.StateCost = MeasureStateCost(options, bios1, bios2, tuning);
        }

        Compare(report);
        Evaluate(report);
        return report;
    }

    private static INetplayTransport BuildTransport(ProbeOptions options, bool host)
    {
        INetplayTransport transport = host
            ? new UdpNetplayTransport(options.Port)
            : new UdpNetplayTransport(
                0,
                new IPEndPoint(IPAddress.Loopback, options.Port));
        if (options.DropInputEvery > 0)
        {
            transport = new InputDropNetplayTransport(transport, options.DropInputEvery);
        }

        if (options.LatencyMilliseconds > 0)
        {
            transport = new DelayedNetplayTransport(transport, options.LatencyMilliseconds);
        }

        return transport;
    }

    private static PeerRunReport[] RunPair(
        NetplayMachinePeer hostPeer,
        NetplayMachinePeer joinPeer,
        int frames,
        int checkpointInterval)
    {
        var hostTask = Task.Run(() => RunPeer(hostPeer, frames, checkpointInterval));
        var joinTask = Task.Run(() => RunPeer(joinPeer, frames, checkpointInterval));
        Task.WhenAll(hostTask, joinTask).GetAwaiter().GetResult();
        return [hostTask.Result, joinTask.Result];
    }

    private static PeerRunReport RunPeer(
        NetplayMachinePeer peer,
        int frames,
        int checkpointInterval)
    {
        var report = new PeerRunReport
        {
            Role = peer.Session.LocalPlayerIndex == 0 ? "host" : "join",
        };
        var clock = Stopwatch.StartNew();
        report.Started = peer.Session.Start();
        if (report.Started)
        {
            for (var i = 0; i < frames; i++)
            {
                NetplayFrameResult result = peer.RunFrame();
                if (!result.Advanced)
                {
                    break;
                }

                report.Frames++;
                if (result.Frame % checkpointInterval == 0 || result.Frame == frames - 1)
                {
                    report.Checkpoints.Add(Checkpoint(result));
                }
            }

            clock.Stop();
            report.Seconds = clock.Elapsed.TotalSeconds;
            report.FramesPerSecond = report.Seconds > 0 ? report.Frames / report.Seconds : 0;
            if (peer.Session.State == NetplaySessionState.Running)
            {
                peer.Session.Complete();
            }
        }
        else
        {
            clock.Stop();
        }

        report.State = peer.Session.State.ToString();
        report.Completed = report.Started && report.Frames == frames;
        report.Failure = peer.Session.FailureMessage;
        FillMetrics(report, peer.Session);
        if (peer.Session.Desync is { } desync)
        {
            report.DesyncFrame = desync.Frame;
            report.LocalFrameHash = FormattableString.Invariant($"0x{desync.LocalFrameHash:X8}");
            report.RemoteFrameHash = FormattableString.Invariant($"0x{desync.RemoteFrameHash:X8}");
            report.LocalAudioHash = FormattableString.Invariant($"0x{desync.LocalAudioHash:X8}");
            report.RemoteAudioHash = FormattableString.Invariant($"0x{desync.RemoteAudioHash:X8}");
            report.LocalAudioSamples = desync.LocalAudioSamples;
            report.RemoteAudioSamples = desync.RemoteAudioSamples;
        }

        return report;
    }

    private static void FillMetrics(PeerRunReport report, LockstepSession session)
    {
        NetplayMetrics metrics = session.Metrics;
        report.PacketsSent = metrics.PacketsSent;
        report.PacketsReceived = metrics.PacketsReceived;
        report.BytesSent = metrics.BytesSent;
        report.BytesReceived = metrics.BytesReceived;
        report.InputResends = metrics.InputResends;
        report.RedundantInputs = metrics.RedundantInputs;
        report.DuplicateInputs = metrics.DuplicateInputs;
        report.MaxWaitMilliseconds = metrics.MaxWaitMilliseconds;
        report.TotalWaitMilliseconds = metrics.TotalWaitMilliseconds;
    }

    private static ReferenceRunReport RunReference(
        ProbeOptions options,
        byte[] bios1,
        byte[] bios2,
        EmulatorTuning tuning,
        Func<long, ControllerState> player1,
        Func<long, ControllerState> player2,
        int checkpointInterval)
    {
        using IDiscSource disc = DiscImageFactory.Open(options.DiscPath);
        FiveDoMachine machine = CreateMachine(bios1, bios2, disc, tuning);
        var pbus = new PbusSource();
        var audio = new NetplayAudioSink();
        machine.PbusSource = pbus;
        machine.AudioSink = audio;
        FrameBuffer? buffer = null;
        var report = new ReferenceRunReport { Ran = true };
        var clock = Stopwatch.StartNew();
        for (var frame = 0; frame < options.Frames; frame++)
        {
            pbus[0] = player1(frame);
            pbus[1] = player2(frame);
            machine.RunFrame();
            var hash = HashFrame(machine, ref buffer);
            report.Frames++;
            if (frame % checkpointInterval == 0 || frame == options.Frames - 1)
            {
                report.Checkpoints.Add(new CheckpointReport
                {
                    Frame = frame,
                    FrameHash = FormattableString.Invariant($"0x{hash:X8}"),
                    AudioHash = FormattableString.Invariant($"0x{audio.Hash:X8}"),
                    AudioSamples = audio.SampleCount,
                });
                report.FinalFrameHash = FormattableString.Invariant($"0x{hash:X8}");
            }
        }

        clock.Stop();
        report.Seconds = clock.Elapsed.TotalSeconds;
        report.FramesPerSecond = report.Seconds > 0 ? report.Frames / report.Seconds : 0;
        return report;
    }

    private static StateCostReport MeasureStateCost(
        ProbeOptions options,
        byte[] bios1,
        byte[] bios2,
        EmulatorTuning tuning)
    {
        using IDiscSource disc = DiscImageFactory.Open(options.DiscPath);
        FiveDoMachine machine = CreateMachine(bios1, bios2, disc, tuning);
        machine.RunFrames(180);
        var snapshot = new MachineStateSnapshot();
        snapshot.Capture(machine);
        const int iterations = 20;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            snapshot.Capture(machine);
        }

        var capture = clock.Elapsed.TotalMilliseconds / iterations;
        clock.Restart();
        for (var i = 0; i < iterations; i++)
        {
            snapshot.Restore(machine);
        }

        var restore = clock.Elapsed.TotalMilliseconds / iterations;
        var report = new StateCostReport
        {
            SnapshotBytes = snapshot.Length,
            SnapshotCaptureMilliseconds = capture,
            SnapshotRestoreMilliseconds = restore,
        };

        using var stream = new MemoryStream();
        clock.Restart();
        machine.SaveState(stream, discHash: null, compress: true);
        clock.Stop();
        report.CompressedSaveMilliseconds = clock.Elapsed.TotalMilliseconds;
        report.CompressedBytes = checked((int)stream.Length);
        stream.Position = 0;
        clock.Restart();
        machine.LoadState(stream);
        clock.Stop();
        report.CompressedLoadMilliseconds = clock.Elapsed.TotalMilliseconds;
        return report;
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

    private static uint HashFrame(FiveDoMachine machine, ref FrameBuffer? buffer)
    {
        VdlFrame frame = machine.Frame;
        if (buffer is null
            || buffer.Width != frame.SourceWidth
            || buffer.Height != frame.SourceHeight)
        {
            buffer = new FrameBuffer(frame.SourceWidth, frame.SourceHeight);
        }

        FrameAssembler.Assemble(frame, buffer, buffer.Width, buffer.Height);
        return FrameHasher.HashBgra(buffer.PixelSpan);
    }

    private static CheckpointReport Checkpoint(NetplayFrameResult result)
    {
        return new CheckpointReport
        {
            Frame = result.Frame,
            FrameHash = FormattableString.Invariant($"0x{result.FrameHash:X8}"),
            AudioHash = FormattableString.Invariant($"0x{result.AudioHash:X8}"),
            AudioSamples = result.AudioSamples,
        };
    }

    private static void Compare(ProbeReport report)
    {
        Dictionary<long, CheckpointReport> host = report.Host.Checkpoints.ToDictionary(c => c.Frame);
        foreach (CheckpointReport join in report.Join.Checkpoints)
        {
            if (host.TryGetValue(join.Frame, out CheckpointReport? local)
                && (!string.Equals(local.FrameHash, join.FrameHash, StringComparison.Ordinal)
                    || !string.Equals(local.AudioHash, join.AudioHash, StringComparison.Ordinal)
                    || local.AudioSamples != join.AudioSamples))
            {
                report.CheckpointMismatches++;
            }
        }

        if (report.Reference is not { Ran: true } reference)
        {
            return;
        }

        Dictionary<long, CheckpointReport> expected = reference.Checkpoints.ToDictionary(c => c.Frame);
        foreach (CheckpointReport checkpoint in report.Host.Checkpoints)
        {
            if (expected.TryGetValue(checkpoint.Frame, out CheckpointReport? local)
                && (!string.Equals(local.FrameHash, checkpoint.FrameHash, StringComparison.Ordinal)
                    || !string.Equals(local.AudioHash, checkpoint.AudioHash, StringComparison.Ordinal)
                    || local.AudioSamples != checkpoint.AudioSamples))
            {
                report.ReferenceMismatches++;
            }
        }
    }

    private static void Evaluate(ProbeReport report)
    {
        if (!report.Host.Started || !report.Join.Started)
        {
            report.Problems.Add("One or both peers did not complete the handshake.");
        }

        if (!report.Host.Completed || !report.Join.Completed)
        {
            report.Problems.Add("One or both peers did not run the requested frame count.");
        }

        if (report.Host.DesyncFrame is not null || report.Join.DesyncFrame is not null)
        {
            report.Problems.Add("The peers reported a frame or audio desync.");
        }

        if (report.CheckpointMismatches != 0)
        {
            report.Problems.Add($"{report.CheckpointMismatches} host/join checkpoint mismatches.");
        }

        if (report.ReferenceMismatches != 0)
        {
            report.Problems.Add($"{report.ReferenceMismatches} netplay/reference checkpoint mismatches.");
        }

        report.Ok = report.Problems.Count == 0;
    }

    private static ControllerState DefaultPlayer1(long frame)
    {
        return new ControllerState(((frame / 120) % 4) switch
        {
            0 => ControllerButton.A,
            2 => ControllerButton.Left,
            _ => ControllerButton.None,
        });
    }

    private static ControllerState DefaultPlayer2(long frame)
    {
        return new ControllerState(((frame / 90) % 3) switch
        {
            0 => ControllerButton.P,
            1 => ControllerButton.Right,
            _ => ControllerButton.None,
        });
    }
}

internal static class ProbeInput
{
    public static Func<long, ControllerState> Load(
        string? path,
        Func<long, ControllerState> fallback)
    {
        if (path is null)
        {
            return fallback;
        }

        IReadOnlyList<InputScriptEntry> entries = InputScript.Load(path);
        return frame => Find(entries, frame);
    }

    private static ControllerState Find(IReadOnlyList<InputScriptEntry> entries, long frame)
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
}
