using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FiveDo.Host.Netplay;
using FiveDo.Input;
using FiveDo.Video;

namespace FiveDo.Tests.Netplay;

internal sealed class InMemoryNetplayTransport : INetplayTransport
{
    private readonly BlockingCollection<byte[]> _inbox = new();

    public InMemoryNetplayTransport? Peer { get; set; }

    public int? DropAfterPacket { get; set; }

    public int DropEvery { get; set; }

    public int DropInputFrom { get; set; } = -1;

    public int DropInputTo { get; set; } = -1;

    public int SentPackets { get; private set; }

    private int _inputPackets;

    public bool HasRemote => Peer is not null;

    public void Send(ReadOnlySpan<byte> packet)
    {
        SentPackets++;
        if (DropAfterPacket is { } limit && SentPackets > limit)
        {
            return;
        }

        if (DropEvery > 0 && SentPackets % DropEvery == 0)
        {
            return;
        }

        if (NetplayCodec.TryPeekType(packet, out NetplayMessageType type)
            && type == NetplayMessageType.Input)
        {
            _inputPackets++;
            if (_inputPackets >= DropInputFrom && _inputPackets <= DropInputTo)
            {
                return;
            }
        }

        Peer?._inbox.Add(packet.ToArray());
    }

    public bool TryReceive(Span<byte> destination, out int length)
    {
        if (_inbox.TryTake(out byte[]? packet))
        {
            packet.CopyTo(destination);
            length = packet.Length;
            return true;
        }

        length = 0;
        return false;
    }

    public void Dispose() => _inbox.Dispose();
}

public sealed class NetplaySessionTests
{
    private const long Frames = 600;
    private const ulong SessionId = 0x5D0_0000_0000_0001;
    private const ulong ContentHash = 0x1234_5678_9ABC_DEF0;

    [Fact]
    public async Task Session_AppliesBothScriptsAndStaysInSync()
    {
        (LockstepSession host, LockstepSession join) = CreatePair();
        List<(long Frame, ControllerState Local, ControllerState Remote)> hostApplied = [];
        List<(long Frame, ControllerState Local, ControllerState Remote)> joinApplied = [];

        var hostTask = Task.Run(() => RunPeer(host, hostApplied));
        var joinTask = Task.Run(() => RunPeer(join, joinApplied));
        bool[] results = await Task.WhenAll(hostTask, joinTask).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(results[0], host.FailureMessage);
        Assert.True(results[1], join.FailureMessage);
        Assert.Null(host.Desync);
        Assert.Null(join.Desync);
        Assert.Equal(NetplaySessionState.Completed, host.State);
        Assert.Equal(NetplaySessionState.Completed, join.State);
        Assert.Equal(Frames, hostApplied.Count);
        Assert.Equal(Frames, joinApplied.Count);
        Assert.True(host.Metrics.PacketsSent > Frames);
        Assert.True(join.Metrics.PacketsReceived > Frames);

        for (var i = 0; i < hostApplied.Count; i++)
        {
            Assert.Equal(hostApplied[i].Frame, joinApplied[i].Frame);
            Assert.Equal(hostApplied[i].Local, P1Script(hostApplied[i].Frame));
            Assert.Equal(hostApplied[i].Remote, P2Script(hostApplied[i].Frame));
            Assert.Equal(joinApplied[i].Local, P2Script(joinApplied[i].Frame));
            Assert.Equal(joinApplied[i].Remote, P1Script(joinApplied[i].Frame));
        }
    }

    [Fact]
    public async Task Session_RecoversFromEveryThirdPacketLoss()
    {
        var hostTransport = new InMemoryNetplayTransport();
        var joinTransport = new InMemoryNetplayTransport();
        hostTransport.Peer = joinTransport;
        joinTransport.Peer = hostTransport;
        hostTransport.DropEvery = 3;
        joinTransport.DropEvery = 3;
        LockstepSession host = CreateSession(hostTransport, NetplayRole.Host, P1Script);
        LockstepSession join = CreateSession(joinTransport, NetplayRole.Join, P2Script);

        var hostTask = Task.Run(() => RunPeer(host, []));
        var joinTask = Task.Run(() => RunPeer(join, []));
        bool[] results = await Task.WhenAll(hostTask, joinTask).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(results[0], host.FailureMessage);
        Assert.True(results[1], join.FailureMessage);
        Assert.Null(host.Desync);
        Assert.Null(join.Desync);
        Assert.Equal(NetplaySessionState.Completed, host.State);
        Assert.Equal(NetplaySessionState.Completed, join.State);
    }

    [Fact]
    public async Task Session_RecoversFromBurstLossWithInputRequest()
    {
        var hostTransport = new InMemoryNetplayTransport();
        var joinTransport = new InMemoryNetplayTransport();
        hostTransport.Peer = joinTransport;
        joinTransport.Peer = hostTransport;
        joinTransport.DropInputFrom = 300;
        joinTransport.DropInputTo = 330;
        LockstepSession host = CreateSession(hostTransport, NetplayRole.Host, P1Script);
        LockstepSession join = CreateSession(joinTransport, NetplayRole.Join, P2Script);

        var hostTask = Task.Run(() => RunPeer(host, []));
        var joinTask = Task.Run(() => RunPeer(join, []));
        bool[] results = await Task.WhenAll(hostTask, joinTask).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(results[0], host.FailureMessage);
        Assert.True(results[1], join.FailureMessage);
        Assert.Null(host.Desync);
        Assert.Null(join.Desync);
        Assert.True(
            host.Metrics.PacketsSent > (2 * Frames) + 10,
            $"expected input requests, sent {host.Metrics.PacketsSent}");
        Assert.Equal(NetplaySessionState.Completed, host.State);
        Assert.Equal(NetplaySessionState.Completed, join.State);
    }

    [Fact]
    public async Task Session_ReportsDesyncWhenCheckpointHashesDiffer()
    {
        (LockstepSession host, LockstepSession join) = CreatePair();
        var hostTask = Task.Run(() => RunPeer(host, [], corruptFrame: -1));
        var joinTask = Task.Run(() => RunPeer(join, [], corruptFrame: 300));
        _ = await Task.WhenAll(hostTask, joinTask).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(
            host.Desync is not null || join.Desync is not null,
            $"host={host.State} join={join.State}");
        Assert.NotEqual(NetplaySessionState.Completed, host.State);
        Assert.NotEqual(NetplaySessionState.Completed, join.State);
    }

    [Fact]
    public async Task Session_TimesOutWhenRemotePacketsStop()
    {
        var hostTransport = new InMemoryNetplayTransport();
        var joinTransport = new InMemoryNetplayTransport();
        hostTransport.Peer = joinTransport;
        joinTransport.Peer = hostTransport;
        joinTransport.DropAfterPacket = 120;
        LockstepSession host = CreateSession(hostTransport, NetplayRole.Host, P1Script);
        LockstepSession join = CreateSession(joinTransport, NetplayRole.Join, P2Script);

        var hostTask = Task.Run(() => RunPeer(host, []));
        var joinTask = Task.Run(() => RunPeer(join, []));
        bool[] results = await Task.WhenAll(hostTask, joinTask).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(results[0]);
        Assert.Equal(NetplaySessionState.TimedOut, host.State);
        Assert.Contains("Timed out", host.FailureMessage, StringComparison.Ordinal);
        Assert.True(host.Frame < Frames);
    }

    private static (LockstepSession Host, LockstepSession Join) CreatePair()
    {
        var hostTransport = new InMemoryNetplayTransport();
        var joinTransport = new InMemoryNetplayTransport();
        hostTransport.Peer = joinTransport;
        joinTransport.Peer = hostTransport;
        return (
            CreateSession(hostTransport, NetplayRole.Host, P1Script),
            CreateSession(joinTransport, NetplayRole.Join, P2Script));
    }

    private static LockstepSession CreateSession(
        InMemoryNetplayTransport transport,
        NetplayRole role,
        Func<long, ControllerState> script)
    {
        return new LockstepSession(
            transport,
            new NetplayOptions
            {
                Role = role,
                InputDelayFrames = 2,
                SessionId = SessionId,
                ContentHash = ContentHash,
                FirstFrame = 0,
                StepTimeout = TimeSpan.FromSeconds(5),
            },
            script);
    }

    private static bool RunPeer(
        LockstepSession session,
        List<(long Frame, ControllerState Local, ControllerState Remote)> applied,
        long corruptFrame = -1)
    {
        if (!session.Start())
        {
            return false;
        }

        for (var i = 0; i < Frames; i++)
        {
            if (!session.Step())
            {
                return session.State == NetplaySessionState.Completed;
            }

            var frame = session.Frame - 1;
            applied.Add((frame, session.LocalInput, session.RemoteInput));
            ControllerState player1 = session.LocalPlayerIndex == 0 ? session.LocalInput : session.RemoteInput;
            ControllerState player2 = session.LocalPlayerIndex == 0 ? session.RemoteInput : session.LocalInput;
            var hash = frame == corruptFrame ? 0xDEADBEEF : HashFrame(frame, player1, player2);
            session.PublishHash(frame, hash, 0, 0);
        }

        session.Complete();
        return true;
    }

    private static uint HashFrame(long frame, ControllerState player1, ControllerState player2)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)frame);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[4..], (ushort)player1.Buttons);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[6..], (ushort)player2.Buttons);
        return FrameHasher.HashBytes(bytes);
    }

    private static ControllerState P1Script(long frame)
    {
        return new ControllerState(
            (frame / 60) % 2 == 0 ? ControllerButton.A : ControllerButton.None);
    }

    private static ControllerState P2Script(long frame)
    {
        return new ControllerState(
            (frame / 90) % 2 == 0 ? ControllerButton.Left : ControllerButton.Right);
    }
}
