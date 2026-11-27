using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Host.Netplay;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Netplay;

public sealed class NetplayMachineTests
{
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
        var hostTransport = new InMemoryNetplayTransport();
        var joinTransport = new InMemoryNetplayTransport();
        hostTransport.Peer = joinTransport;
        joinTransport.Peer = hostTransport;
        const ulong contentHash = 0x2222_3333_4444_5555;
        var hostSession = new LockstepSession(
            hostTransport,
            new NetplayOptions
            {
                Role = NetplayRole.Host,
                InputDelayFrames = 2,
                SessionId = 42,
                ContentHash = contentHash,
                StepTimeout = TimeSpan.FromSeconds(10),
            },
            frame => new ControllerState((frame / 30) % 2 == 0 ? ControllerButton.A : ControllerButton.None));
        var joinSession = new LockstepSession(
            joinTransport,
            new NetplayOptions
            {
                Role = NetplayRole.Join,
                InputDelayFrames = 2,
                SessionId = 42,
                ContentHash = contentHash,
                StepTimeout = TimeSpan.FromSeconds(10),
            },
            frame => new ControllerState((frame / 45) % 2 == 0 ? ControllerButton.Left : ControllerButton.Right));

        using var hostPeer = new NetplayMachinePeer(CreateSyntheticMachine(), hostSession);
        using var joinPeer = new NetplayMachinePeer(CreateSyntheticMachine(), joinSession);
        var hostHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();
        var joinHashes = new ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)>();

        bool[] results = await RunPairAsync(hostPeer, joinPeer, hostHashes, joinHashes);

        Assert.True(results[0], hostSession.FailureMessage);
        Assert.True(results[1], joinSession.FailureMessage);
        Assert.Null(hostSession.Desync);
        Assert.Null(joinSession.Desync);
        Assert.Equal(300, hostHashes.Count);
        Assert.Equal(300, joinHashes.Count);
        Assert.Equal(hostHashes.ToArray(), joinHashes.ToArray());
    }

    private static async Task<bool[]> RunPairAsync(
        NetplayMachinePeer hostPeer,
        NetplayMachinePeer joinPeer,
        ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)> hostHashes,
        ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)> joinHashes)
    {
        var hostTask = Task.Run(() => RunPeer(hostPeer, hostHashes));
        var joinTask = Task.Run(() => RunPeer(joinPeer, joinHashes));
        return await Task.WhenAll(hostTask, joinTask)
            .WaitAsync(TimeSpan.FromSeconds(60))
            .ConfigureAwait(false);
    }

    private static bool RunPeer(
        NetplayMachinePeer peer,
        ConcurrentQueue<(long Frame, uint FrameHash, uint AudioHash)> hashes)
    {
        if (!peer.Session.Start())
        {
            return false;
        }

        for (var i = 0; i < 300; i++)
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

    private static FiveDoMachine CreateSyntheticMachine()
    {
        uint[] program =
        [
            ArmTestAssembler.Mov(0, 0x03300000),
            ArmTestAssembler.Mov(1, 0x000B0000),
            ArmTestAssembler.StrImmediate(1, 0, 0x580),
            ArmTestAssembler.Mov(2, 0x03200000),
            ArmTestAssembler.AddImmediate(2, 2, 0x2000),
            ArmTestAssembler.Mvn(3, 0),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Branch(0x0300001c, 0x03000018),
        ];

        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(rom.AsSpan(i * 4), program[i]);
        }

        var machine = new FiveDoMachine();
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }
}
