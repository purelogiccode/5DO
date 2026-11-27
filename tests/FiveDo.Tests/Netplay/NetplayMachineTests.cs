using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FiveDo.Host.Netplay;
using FiveDo.Input;
using FiveDo.Machine;

namespace FiveDo.Tests.Netplay;

public sealed class NetplayMachineTests
{
    private const ulong ContentHash = 0x2222_3333_4444_5555;

    [Fact]
    public void PbusSource_EncodesBothPlayers()
    {
        var source = new NetplayPbusSource();
        source.SetControllers(
            new ControllerState(ControllerButton.A | ControllerButton.Left),
            new ControllerState(ControllerButton.P | ControllerButton.Right));

        Span<byte> packet = stackalloc byte[PbusEncoder.PacketSize];
        var length = source.GetPbusData(packet);
        Assert.Equal(PbusEncoder.PacketSize, length);
        ControllerState[] decoded = PbusDecoder.Decode(packet);
        Assert.Equal(ControllerButton.A | ControllerButton.Left, decoded[0].Buttons);
        Assert.Equal(ControllerButton.P | ControllerButton.Right, decoded[1].Buttons);
    }

    [Fact]
    public async Task MachinePeers_ProduceEqualFrameAndAudioHashes()
    {
        (NetplayMachinePeer hostPeer, NetplayMachinePeer joinPeer) = CreatePair(
            NetplayTestMachines.CreateSyntheticMachine(),
            NetplayTestMachines.CreateSyntheticMachine(),
            firstFrame: 0,
            negotiated: false);
        using (hostPeer)
        using (joinPeer)
        {
            var hostHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
            var joinHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
            bool[] results = await RunPairAsync(hostPeer, joinPeer, hostHashes, joinHashes, 300, false);

            Assert.True(results[0], hostPeer.Session.FailureMessage);
            Assert.True(results[1], joinPeer.Session.FailureMessage);
            Assert.Null(hostPeer.Session.Desync);
            Assert.Null(joinPeer.Session.Desync);
            Assert.Equal(300, hostHashes.Count);
            Assert.Equal(300, joinHashes.Count);
            Assert.Equal(hostHashes.ToArray(), joinHashes.ToArray());
        }
    }

    [Fact]
    public async Task MachinePeers_ResumeFromTransferredState()
    {
        FiveDoMachine hostMachine = NetplayTestMachines.CreateSyntheticMachine();
        FiveDoMachine joinMachine = NetplayTestMachines.CreateSyntheticMachine();
        (NetplayMachinePeer firstHost, NetplayMachinePeer firstJoin) = CreatePair(
            hostMachine,
            joinMachine,
            firstFrame: 0,
            negotiated: false);
        using (firstHost)
        using (firstJoin)
        {
            var hostHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
            var joinHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
            bool[] first = await RunPairAsync(firstHost, firstJoin, hostHashes, joinHashes, 180, false);
            Assert.True(first[0], firstHost.Session.FailureMessage);
            Assert.True(first[1], firstJoin.Session.FailureMessage);
            Assert.Equal(hostHashes.ToArray(), joinHashes.ToArray());
        }

        Assert.Equal(180, hostMachine.FramesExecuted);
        NetplayStateMessage state = NetplayStateTransfer.Capture(hostMachine, null);
        Assert.True(NetplayStateTransfer.TryApply(joinMachine, state, null, out string? error), error);
        Assert.Equal(180, joinMachine.FramesExecuted);

        (NetplayMachinePeer hostPeer, NetplayMachinePeer joinPeer) = CreatePair(
            hostMachine,
            joinMachine,
            firstFrame: 180,
            negotiated: true);
        using (hostPeer)
        using (joinPeer)
        {
            var hostHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
            var joinHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
            bool[] resumed = await RunPairAsync(hostPeer, joinPeer, hostHashes, joinHashes, 120, true);
            Assert.True(resumed[0], hostPeer.Session.FailureMessage);
            Assert.True(resumed[1], joinPeer.Session.FailureMessage);
            Assert.Null(hostPeer.Session.Desync);
            Assert.Null(joinPeer.Session.Desync);
            Assert.Equal(120, hostHashes.Count);
            Assert.Equal(120, joinHashes.Count);
            Assert.Equal(hostHashes.ToArray(), joinHashes.ToArray());
            Assert.Equal(180, hostHashes.ToArray()[0].Frame);
        }
    }

    private static (NetplayMachinePeer Host, NetplayMachinePeer Join) CreatePair(
        FiveDoMachine hostMachine,
        FiveDoMachine joinMachine,
        long firstFrame,
        bool negotiated)
    {
        var hostTransport = new InMemoryNetplayTransport();
        var joinTransport = new InMemoryNetplayTransport();
        hostTransport.Peer = joinTransport;
        joinTransport.Peer = hostTransport;
        var hostSession = CreateSession(hostTransport, NetplayRole.Host, firstFrame);
        var joinSession = CreateSession(joinTransport, NetplayRole.Join, firstFrame);
        var hostPeer = new NetplayMachinePeer(hostMachine, hostSession);
        var joinPeer = new NetplayMachinePeer(joinMachine, joinSession);
        if (negotiated)
        {
            Assert.True(hostSession.StartNegotiated());
            Assert.True(joinSession.StartNegotiated());
        }

        return (hostPeer, joinPeer);
    }

    private static LockstepSession CreateSession(
        InMemoryNetplayTransport transport,
        NetplayRole role,
        long firstFrame)
    {
        return new LockstepSession(
            transport,
            new NetplayOptions
            {
                Role = role,
                InputDelayFrames = 2,
                SessionId = 42,
                ContentHash = ContentHash,
                FirstFrame = firstFrame,
                StepTimeout = TimeSpan.FromSeconds(10),
            },
            frame => new ControllerState(role == NetplayRole.Host
                ? ((frame / 30) % 2 == 0 ? ControllerButton.A : ControllerButton.None)
                : ((frame / 45) % 2 == 0 ? ControllerButton.Left : ControllerButton.Right)));
    }

    private static async Task<bool[]> RunPairAsync(
        NetplayMachinePeer hostPeer,
        NetplayMachinePeer joinPeer,
        ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)> hostHashes,
        ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)> joinHashes,
        int frames,
        bool negotiated)
    {
        var hostTask = Task.Run(() => RunPeer(hostPeer, hostHashes, frames, negotiated));
        var joinTask = Task.Run(() => RunPeer(joinPeer, joinHashes, frames, negotiated));
        return await Task.WhenAll(hostTask, joinTask)
            .WaitAsync(TimeSpan.FromSeconds(60))
            .ConfigureAwait(false);
    }

    private static bool RunPeer(
        NetplayMachinePeer peer,
        ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)> hashes,
        int frames,
        bool negotiated)
    {
        if (!negotiated && !peer.Session.Start())
        {
            return false;
        }

        for (var i = 0; i < frames; i++)
        {
            NetplayFrameResult result = peer.RunFrame();
            if (!result.Advanced)
            {
                return peer.Session.State == NetplaySessionState.Completed;
            }

            hashes.Enqueue((result.Frame, result.FrameHash, result.AudioHash));
        }

        peer.Session.Complete();
        return true;
    }
}
