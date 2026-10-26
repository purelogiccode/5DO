using FiveDo.Madam;
using FiveDo.Memory;

namespace FiveDo.Tests.Madam;

public sealed class CelBitReaderTests
{
    [Fact]
    public void Read_OneBitAtATime()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xb2, 0x4d);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);
        reader.SetBitRate(1);

        var expected = new uint[] { 1, 0, 1, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 1 };
        foreach (var bit in expected)
        {
            Assert.Equal(bit, reader.Read());
        }
    }

    [Fact]
    public void Read_Nibbles()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xab, 0xcd);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);
        reader.SetBitRate(4);

        Assert.Equal(0xau, reader.Read());
        Assert.Equal(0xbu, reader.Read());
        Assert.Equal(0xcu, reader.Read());
        Assert.Equal(0xdu, reader.Read());
    }

    [Fact]
    public void Read_BytesHalfwordsAndWords()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0x12, 0x34, 0x56, 0x78);
        var reader = new CelBitReader(memory.Ram);

        reader.AttachBuffer(0x1000);
        reader.SetBitRate(8);
        Assert.Equal(0x12u, reader.Read());
        Assert.Equal(0x34u, reader.Read());

        reader.AttachBuffer(0x1000);
        reader.SetBitRate(16);
        Assert.Equal(0x1234u, reader.Read());

        reader.AttachBuffer(0x1000);
        reader.SetBitRate(32);
        Assert.Equal(0x12345678u, reader.Read());
    }

    [Fact]
    public void Read_ThreeBitValuesCrossByteBoundary()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xa5, 0x3c);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);
        reader.SetBitRate(3);

        var expected = new uint[] { 5, 1, 2, 3, 6 };
        foreach (var value in expected)
        {
            Assert.Equal(value, reader.Read());
        }
    }

    [Fact]
    public void Skip_AdvancesBitPosition()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xa5);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);
        reader.SetBitRate(4);

        reader.Skip(4);
        Assert.Equal(0x5u, reader.Read());
        Assert.Equal(1u, reader.BytePosition);
    }

    [Fact]
    public void SetPos_IsEquivalentToSetPosition()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xa5);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);
        reader.SetBitRate(4);

        reader.SetPos(4);
        Assert.Equal(0x5u, reader.Read());

        reader.SetPosition(0, 4);
        Assert.Equal(0x5u, reader.Read());
    }

    [Fact]
    public void GetBytePose_TracksBytePosition()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xa5, 0x3c);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);
        reader.SetBitRate(4);

        reader.Read();
        reader.Read();
        Assert.Equal(1u, reader.BytePosition);
        Assert.Equal(0, reader.BitPosition);

        reader.Read();
        Assert.Equal(1u, reader.BytePosition);
        Assert.Equal(4, reader.BitPosition);
    }

    [Fact]
    public void ZeroBuffer_ReturnsZero()
    {
        var memory = new FiveDoMemory();
        var reader = new CelBitReader(memory.Ram);
        reader.SetBitRate(8);

        Assert.Equal(0u, reader.Read());
    }

    [Fact]
    public void SetBitRate_ClampsToValidRange()
    {
        var memory = new FiveDoMemory();
        WriteBytes(memory, 0x1000, 0xa5, 0x3c, 0x00, 0x00);
        var reader = new CelBitReader(memory.Ram);
        reader.AttachBuffer(0x1000);

        reader.SetBitRate(0);
        Assert.Equal(1, reader.BitRate);
        Assert.Equal(1u, reader.Read());

        reader.SetBitRate(40);
        Assert.Equal(32, reader.BitRate);
        reader.SetPos(0);
        Assert.Equal(0xa53c0000u, reader.Read());
    }

    private static void WriteBytes(FiveDoMemory memory, uint buffer, params byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            memory.Ram.Write8(buffer + ((uint)i ^ 3), bytes[i]);
        }
    }
}
