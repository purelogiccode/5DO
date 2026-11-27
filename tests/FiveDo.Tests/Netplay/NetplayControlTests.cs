using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Host.Netplay;
using FiveDo.Video;

namespace FiveDo.Tests.Netplay;

public sealed class NetplayControlTests
{
    [Fact]
    public void JoinRequest_RoundTrips()
    {
        var expected = new NetplayJoinRequest(
            NetplayJoinKind.Reconnect,
            0x0123456789ABCDEF,
            0x1122334455667788,
            1234,
            0xDEADBEEF,
            4,
            2,
            "disc-id");
        byte[] payload = NetplayControlCodec.Encode(expected);

        Assert.True(NetplayControlCodec.TryDecodeJoinRequest(payload, out NetplayJoinRequest actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JoinResponse_RoundTrips()
    {
        var expected = new NetplayJoinResponse(
            true,
            NetplayControlReason.Accepted,
            42,
            0x1234,
            99,
            4,
            2,
            "ready");
        byte[] payload = NetplayControlCodec.Encode(expected);

        Assert.True(NetplayControlCodec.TryDecodeJoinResponse(payload, out NetplayJoinResponse actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void State_RoundTripsLargePayload()
    {
        var state = new NetplayStateMessage(3600, 0x110E7DC5, new byte[700_000]);
        state.Data[0] = 0x5A;
        state.Data[^1] = 0xA5;
        byte[] payload = NetplayControlCodec.Encode(state);

        Assert.True(NetplayControlCodec.TryDecodeState(payload, out NetplayStateMessage? actual));
        Assert.NotNull(actual);
        Assert.Equal(state.StartFrame, actual.StartFrame);
        Assert.Equal(state.FrameHash, actual.FrameHash);
        Assert.Equal(state.Data.Length, actual.Data.Length);
        Assert.Equal(state.Data[0], actual.Data[0]);
        Assert.Equal(state.Data[^1], actual.Data[^1]);
    }

    [Fact]
    public void StateAckAndClose_RoundTrip()
    {
        var ack = new NetplayStateAck(3600, 0x110E7DC5, true, "ok");
        Assert.True(NetplayControlCodec.TryDecodeStateAck(
            NetplayControlCodec.Encode(ack),
            out NetplayStateAck decodedAck));
        Assert.Equal(ack, decodedAck);

        var close = new NetplayCloseMessage(NetplayControlReason.PeerClosed, "done");
        Assert.True(NetplayControlCodec.TryDecodeClose(
            NetplayControlCodec.Encode(close),
            out NetplayCloseMessage decodedClose));
        Assert.Equal(close, decodedClose);
    }

    [Fact]
    public void Decoder_RejectsWrongTypeTruncationAndTrailingData()
    {
        var request = new NetplayJoinRequest(
            NetplayJoinKind.Fresh,
            1,
            2,
            3,
            4,
            2,
            2,
            "disc");
        byte[] payload = NetplayControlCodec.Encode(request);
        Assert.False(NetplayControlCodec.TryDecodeJoinResponse(payload, out _));
        Assert.False(NetplayControlCodec.TryDecodeJoinRequest(payload.AsSpan(0, payload.Length - 1), out _));

        byte[] trailing = new byte[payload.Length + 1];
        payload.CopyTo(trailing, 0);
        Assert.False(NetplayControlCodec.TryDecodeJoinRequest(trailing, out _));
    }

    [Fact]
    public async Task TcpChannel_FramesJoinRequestAndState()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var listener = new NetplayControlListener(0, IPAddress.Loopback);
        int port = listener.LocalEndPoint.Port;
        Task<NetplayControlChannel> accept = listener.AcceptAsync(cts.Token);
        using NetplayControlChannel client = await NetplayControlChannel
            .ConnectAsync("127.0.0.1", port, cts.Token)
            ;
        using NetplayControlChannel server = await accept;

        var request = new NetplayJoinRequest(
            NetplayJoinKind.Fresh,
            0x1234,
            0,
            0,
            0,
            4,
            2,
            "disc");
        await client.SendAsync(NetplayControlCodec.Encode(request), cts.Token);
        byte[]? received = await server.ReceiveAsync(cts.Token);
        Assert.NotNull(received);
        Assert.True(NetplayControlCodec.TryDecodeJoinRequest(received, out NetplayJoinRequest decoded));
        Assert.Equal(request, decoded);

        var state = new NetplayStateMessage(120, 0xCAFEBABE, new byte[650_000]);
        Task sendState = server.SendAsync(NetplayControlCodec.Encode(state), cts.Token);
        byte[]? receivedState = await client.ReceiveAsync(cts.Token);
        await sendState;
        Assert.NotNull(receivedState);
        Assert.True(NetplayControlCodec.TryDecodeState(receivedState, out NetplayStateMessage? decodedState));
        Assert.NotNull(decodedState);
        Assert.Equal(state.StartFrame, decodedState.StartFrame);
        Assert.Equal(state.FrameHash, decodedState.FrameHash);
        Assert.Equal(state.Data.Length, decodedState.Data.Length);
    }

    [Fact]
    public void StateTransfer_RestoresExactMachineState()
    {
        var host = NetplayTestMachines.CreateSyntheticMachine();
        host.RunFrames(120);
        NetplayStateMessage blob = NetplayStateTransfer.Capture(host, null);

        var join = NetplayTestMachines.CreateSyntheticMachine();
        Assert.True(
            NetplayStateTransfer.TryApply(join, blob, null, out string? error),
            error);
        Assert.Equal(host.FramesExecuted, join.FramesExecuted);
        Assert.Equal(FrameHasher.Hash(host.Frame), FrameHasher.Hash(join.Frame));

        host.RunFrames(60);
        join.RunFrames(60);
        Assert.Equal(FrameHasher.Hash(host.Frame), FrameHasher.Hash(join.Frame));
    }

    [Fact]
    public void StateTransfer_RejectsTuningOrStateMismatch()
    {
        var host = NetplayTestMachines.CreateSyntheticMachine();
        host.RunFrames(30);
        NetplayStateMessage blob = NetplayStateTransfer.Capture(host, "disc-a");

        var wrongDisc = NetplayTestMachines.CreateSyntheticMachine();
        Assert.False(NetplayStateTransfer.TryApply(wrongDisc, blob, "disc-b", out string? error));
        Assert.NotNull(error);

        var corrupted = new NetplayStateMessage(
            blob.StartFrame,
            blob.FrameHash ^ 1,
            blob.Data);
        var join = NetplayTestMachines.CreateSyntheticMachine();
        Assert.False(NetplayStateTransfer.TryApply(join, corrupted, "disc-a", out error));
        Assert.NotNull(error);
        Assert.Contains("frame hash", error, StringComparison.OrdinalIgnoreCase);
    }
}
