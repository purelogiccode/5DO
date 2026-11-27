using System;
using FiveDo.Host.Netplay;
using FiveDo.Input;

namespace FiveDo.Tests.Netplay;

public sealed class NetplayCodecTests
{
    [Fact]
    public void Hello_RoundTrips()
    {
        var expected = new NetplayHello(
            0x0123456789ABCDEF,
            0xFEDCBA9876543210,
            1234,
            NetplayRole.Join,
            3,
            2);
        var packet = new byte[NetplayCodec.MaxPacketSize];
        var length = NetplayCodec.WriteHello(packet, NetplayMessageType.Hello, expected);

        Assert.True(NetplayCodec.TryReadHello(
            packet.AsSpan(0, length),
            NetplayMessageType.Hello,
            out NetplayHello actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Input_RoundTripsHistory()
    {
        NetplayInputEntry[] expected =
        [
            new(120, ControllerButton.A | ControllerButton.Left),
            new(121, ControllerButton.None),
            new(122, ControllerButton.P | ControllerButton.Right),
        ];
        var packet = new byte[NetplayCodec.MaxPacketSize];
        var length = NetplayCodec.WriteInput(packet, 1, expected);

        Span<NetplayInputEntry> actual = stackalloc NetplayInputEntry[NetplayCodec.InputRedundancy];
        Assert.True(NetplayCodec.TryReadInput(
            packet.AsSpan(0, length),
            out var player,
            actual,
            out var count));
        Assert.Equal(1, player);
        Assert.Equal(expected.Length, count);
        Assert.Equal(expected, actual[..count].ToArray());
    }

    [Fact]
    public void Hash_RoundTrips()
    {
        var expected = new NetplayHash(3600, 0x110E7DC5, 0x725DC753, 3_600_000);
        var packet = new byte[NetplayCodec.MaxPacketSize];
        var length = NetplayCodec.WriteHash(packet, expected);

        Assert.True(NetplayCodec.TryReadHash(packet.AsSpan(0, length), out NetplayHash actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Bye_RoundTrips()
    {
        var packet = new byte[NetplayCodec.MaxPacketSize];
        var length = NetplayCodec.WriteBye(packet, NetplayByeReason.Desync);

        Assert.True(NetplayCodec.TryReadBye(packet.AsSpan(0, length), out NetplayByeReason reason));
        Assert.Equal(NetplayByeReason.Desync, reason);
    }

    [Fact]
    public void InputRequest_RoundTrips()
    {
        var packet = new byte[NetplayCodec.MaxPacketSize];
        var length = NetplayCodec.WriteInputRequest(packet, 1234);

        Assert.True(NetplayCodec.TryReadInputRequest(packet.AsSpan(0, length), out var frame));
        Assert.Equal(1234, frame);
        Assert.False(NetplayCodec.TryReadInputRequest(packet.AsSpan(0, length - 1), out _));
    }

    [Fact]
    public void Reader_RejectsBadMagicVersionLengthAndTruncation()
    {
        var packet = new byte[NetplayCodec.MaxPacketSize];
        var length = NetplayCodec.WriteHash(packet, new NetplayHash(1, 2, 3, 4));

        byte[] badMagic = packet[..length];
        badMagic[0] ^= 0xFF;
        Assert.False(NetplayCodec.TryReadHash(badMagic, out _));

        byte[] badVersion = packet[..length];
        badVersion[4]++;
        Assert.False(NetplayCodec.TryReadHash(badVersion, out _));

        byte[] badLength = packet[..length];
        badLength[6]++;
        Assert.False(NetplayCodec.TryReadHash(badLength, out _));

        Assert.False(NetplayCodec.TryReadHash(packet.AsSpan(0, length - 1), out _));
        Assert.False(NetplayCodec.TryReadHash([], out _));
    }

    [Fact]
    public void Input_RejectsEmptyAndOversizedHistories()
    {
        var packet = new byte[NetplayCodec.MaxPacketSize];
        Assert.Throws<ArgumentException>(() =>
            NetplayCodec.WriteInput(packet, 0, ReadOnlySpan<NetplayInputEntry>.Empty));

        var tooMany = new NetplayInputEntry[NetplayCodec.InputRedundancy + 1];
        Assert.Throws<ArgumentException>(() => NetplayCodec.WriteInput(packet, 0, tooMany));
    }

    [Fact]
    public void ContentHash_IsStableAndCoversBiosDiscAndTuning()
    {
        byte[] bios = [1, 2, 3, 4];
        byte[] bios2 = [5, 6];
        var tuning = FiveDo.Core.EmulatorTuning.Default;
        var baseline = NetplayContentHash.Compute(bios, bios2, "disc-a", tuning);

        Assert.Equal(baseline, NetplayContentHash.Compute(bios, bios2, "disc-a", tuning));
        Assert.NotEqual(baseline, NetplayContentHash.Compute([9, 2, 3, 4], bios2, "disc-a", tuning));
        Assert.NotEqual(baseline, NetplayContentHash.Compute(bios, bios2, "disc-b", tuning));
        Assert.NotEqual(
            baseline,
            NetplayContentHash.Compute(bios, bios2, "disc-a", tuning with { ArmClock = 12_500_001 }));
    }
}
