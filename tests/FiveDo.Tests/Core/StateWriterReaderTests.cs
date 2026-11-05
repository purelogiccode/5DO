using System;
using System.Buffers.Binary;
using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class StateWriterReaderTests
{
    [Fact]
    public void Primitives_Roundtrip()
    {
        var writer = new StateWriter();
        writer.WriteByte(0x12);
        writer.WriteUInt16(0x3456);
        writer.WriteUInt32(0x789ABCDE);
        writer.WriteUInt64(0x0123456789ABCDEF);
        writer.WriteInt32(-123456);
        writer.WriteInt64(-9876543210);
        writer.WriteSingle(1.5f);
        writer.WriteDouble(-2.25);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteBytes(new byte[] { 1, 2, 3, 4 });

        var reader = new StateReader(writer.ToArray());

        Assert.Equal(0x12, reader.ReadByte());
        Assert.Equal(0x3456, reader.ReadUInt16());
        Assert.Equal(0x789ABCDEu, reader.ReadUInt32());
        Assert.Equal(0x0123456789ABCDEFul, reader.ReadUInt64());
        Assert.Equal(-123456, reader.ReadInt32());
        Assert.Equal(-9876543210, reader.ReadInt64());
        Assert.Equal(1.5f, reader.ReadSingle());
        Assert.Equal(-2.25, reader.ReadDouble());
        Assert.True(reader.ReadBoolean());
        Assert.False(reader.ReadBoolean());

        var bytes = new byte[4];
        reader.ReadBytes(bytes);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void String_Roundtrip()
    {
        var writer = new StateWriter();
        writer.WriteString("3DO \u30c6\u30b9\u30c8");
        writer.WriteString(string.Empty);

        var reader = new StateReader(writer.ToArray());

        Assert.Equal("3DO \u30c6\u30b9\u30c8", reader.ReadString());
        Assert.Equal(string.Empty, reader.ReadString());
    }

    [Fact]
    public void WriteString_Null_Throws()
    {
        var writer = new StateWriter();

        Assert.Throws<ArgumentNullException>(() => writer.WriteString(null!));
    }

    [Fact]
    public void Section_Roundtrip()
    {
        var section = new TestStateSection
        {
            Value = unchecked((int)0xDEADBEEF),
            Text = "hello",
        };
        var writer = new StateWriter();
        writer.WriteSection(section);

        var reader = new StateReader(writer.ToArray());
        Assert.True(reader.TryReadSection(out StateSectionReader? sectionReader));
        Assert.NotNull(sectionReader);
        Assert.Equal(TestStateSection.SectionId, sectionReader.Id);
        Assert.Equal(TestStateSection.SectionVersion, sectionReader.Version);

        var restored = new TestStateSection();
        restored.Load(sectionReader);

        Assert.Equal(unchecked((int)0xDEADBEEF), restored.Value);
        Assert.Equal("hello", restored.Text);
        Assert.False(reader.TryReadSection(out _));
    }

    [Fact]
    public void MultipleSections_AreReadInOrder()
    {
        var writer = new StateWriter();
        writer.WriteSection(new TestStateSection { Value = 1 });
        writer.WriteSection(new TestStateSection { Value = 2 });

        var reader = new StateReader(writer.ToArray());

        Assert.True(reader.TryReadSection(out StateSectionReader? first));
        var firstSection = new TestStateSection();
        firstSection.Load(first);
        Assert.Equal(1, firstSection.Value);

        Assert.True(reader.TryReadSection(out StateSectionReader? second));
        var secondSection = new TestStateSection();
        secondSection.Load(second);
        Assert.Equal(2, secondSection.Value);

        Assert.False(reader.TryReadSection(out _));
    }

    [Fact]
    public void Section_Truncated_Throws()
    {
        var writer = new StateWriter();
        writer.WriteSection(new TestStateSection { Value = 1, Text = "abc" });
        var state = writer.ToArray();
        var truncated = state.AsSpan(0, state.Length - 1).ToArray();

        var reader = new StateReader(truncated);

        Assert.Throws<InvalidStateException>(() => reader.TryReadSection(out _));
    }

    [Fact]
    public void Section_CrcMismatch_Throws()
    {
        var writer = new StateWriter();
        writer.WriteSection(new TestStateSection { Value = 1, Text = "abc" });
        var state = writer.ToArray();

        state[StateFormat.SectionHeaderSize] ^= 0xFF;

        var reader = new StateReader(state);

        Assert.Throws<InvalidStateException>(() => reader.TryReadSection(out _));
    }

    [Fact]
    public void Section_ZeroId_Throws()
    {
        var writer = new StateWriter();

        Assert.Throws<ArgumentException>(() => writer.WriteSection(new TestStateSection { Id = 0 }));
    }

    [Fact]
    public void ReadSection_AtEnd_Throws()
    {
        var reader = new StateReader(Array.Empty<byte>());

        Assert.Throws<InvalidStateException>(() => reader.ReadSection());
    }

    [Fact]
    public void TruncatedPrimitive_Throws()
    {
        var reader = new StateReader(new byte[] { 1, 2, 3 });

        Assert.Throws<InvalidStateException>(() => reader.ReadUInt32());
    }

    [Fact]
    public void ReadBytes_TooLarge_Throws()
    {
        var reader = new StateReader(new byte[] { 1, 2 });

        Assert.Throws<InvalidStateException>(() => reader.ReadBytes(new byte[3]));
    }

    [Fact]
    public void InvalidBoolean_Throws()
    {
        var writer = new StateWriter();
        writer.WriteByte(2);

        var reader = new StateReader(writer.ToArray());

        Assert.Throws<InvalidStateException>(() => reader.ReadBoolean());
    }

    [Fact]
    public void InvalidUtf8_Throws()
    {
        var writer = new StateWriter();
        writer.WriteInt32(2);
        writer.WriteBytes(new byte[] { 0xC3, 0x28 });

        var reader = new StateReader(writer.ToArray());

        Assert.Throws<InvalidStateException>(() => reader.ReadString());
    }

    [Fact]
    public void NegativeStringLength_Throws()
    {
        var writer = new StateWriter();
        writer.WriteInt32(-1);

        var reader = new StateReader(writer.ToArray());

        Assert.Throws<InvalidStateException>(() => reader.ReadString());
    }

    [Fact]
    public void LargeSection_IsCompressedAndRoundtrips()
    {
        var source = new LargeTestSection { Payload = CreatePayload(64 * 1024) };
        var writer = new StateWriter();
        writer.WriteSection(source);
        var state = writer.ToArray();

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(6, 2));
        Assert.Equal(StateFormat.SectionFlagCompressed, flags);

        var reader = new StateReader(state);
        Assert.True(reader.TryReadSection(out StateSectionReader? sectionReader));
        Assert.NotNull(sectionReader);
        Assert.True(sectionReader.Compressed);
        Assert.Equal(source.Payload.Length, sectionReader.PayloadLength);
        Assert.True(sectionReader.StoredLength < sectionReader.PayloadLength);

        var restored = new LargeTestSection();
        restored.Load(sectionReader);
        Assert.Equal(source.Payload.ToArray(), restored.Payload.ToArray());
        Assert.False(reader.TryReadSection(out _));
    }

    [Fact]
    public void CompressionDisabled_WritesUncompressedSection()
    {
        var source = new LargeTestSection { Payload = CreatePayload(64 * 1024) };
        var writer = new StateWriter { CompressionEnabled = false };
        writer.WriteSection(source);
        var state = writer.ToArray();

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(6, 2));
        Assert.Equal(0, flags);

        var reader = new StateReader(state);
        Assert.True(reader.TryReadSection(out StateSectionReader? sectionReader));
        Assert.NotNull(sectionReader);
        Assert.False(sectionReader.Compressed);
        Assert.Equal(source.Payload.Length, sectionReader.PayloadLength);
        Assert.Equal(sectionReader.PayloadLength, sectionReader.StoredLength);
    }

    [Fact]
    public void SmallSection_StaysUncompressed()
    {
        var writer = new StateWriter();
        writer.WriteSection(new LargeTestSection { Payload = new byte[100] });
        var state = writer.ToArray();

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(6, 2));
        Assert.Equal(0, flags);
    }

    [Fact]
    public void IncompressibleSection_StaysUncompressed()
    {
        var writer = new StateWriter();
        writer.WriteSection(new LargeTestSection { Payload = CreateIncompressiblePayload(64 * 1024) });
        var state = writer.ToArray();

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(6, 2));
        Assert.Equal(0, flags);

        var reader = new StateReader(state);
        Assert.True(reader.TryReadSection(out StateSectionReader? sectionReader));
        Assert.NotNull(sectionReader);
        Assert.False(sectionReader.Compressed);
        Assert.Equal(sectionReader.PayloadLength, sectionReader.StoredLength);
    }

    [Fact]
    public void CompressedSection_CorruptPayload_Throws()
    {
        var writer = new StateWriter();
        writer.WriteSection(new LargeTestSection { Payload = CreatePayload(64 * 1024) });
        var state = writer.ToArray();
        state[StateFormat.SectionHeaderSize + StateFormat.CompressedSectionPrefixSize] ^= 0xFF;

        var reader = new StateReader(state);

        Assert.Throws<InvalidStateException>(() => reader.TryReadSection(out _));
    }

    [Fact]
    public void CompressedSection_InvalidLength_Throws()
    {
        var writer = new StateWriter();
        writer.WriteSection(new LargeTestSection { Payload = CreatePayload(64 * 1024) });
        var state = writer.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(state.AsSpan(StateFormat.SectionHeaderSize), -1);

        var reader = new StateReader(state);

        Assert.Throws<InvalidStateException>(() => reader.TryReadSection(out _));
    }

    [Fact]
    public void UnknownSectionFlags_Throws()
    {
        var writer = new StateWriter { CompressionEnabled = false };
        writer.WriteSection(new TestStateSection { Value = 1 });
        var state = writer.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(state.AsSpan(6), 0x0004);

        var reader = new StateReader(state);

        Assert.Throws<InvalidStateException>(() => reader.TryReadSection(out _));
    }

    [Fact]
    public void UncheckedSection_RoundtripsWithoutACrc()
    {
        var writer = new StateWriter { ChecksumsEnabled = false };
        writer.WriteSection(new TestStateSection { Value = 42, Text = "no crc" });
        var state = writer.ToArray();

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(6, 2));
        Assert.Equal(StateFormat.SectionFlagUnchecked, flags);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(state.Length - 4, 4)));

        var reader = new StateReader(state);
        Assert.True(reader.TryReadSection(out StateSectionReader? section));
        Assert.NotNull(section);
        var restored = new TestStateSection();
        restored.Load(section);
        Assert.Equal(42, restored.Value);
        Assert.Equal("no crc", restored.Text);
    }

    [Fact]
    public void UncheckedSection_WithNonzeroCrc_Throws()
    {
        var writer = new StateWriter { ChecksumsEnabled = false };
        writer.WriteSection(new TestStateSection { Value = 1 });
        var state = writer.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(state.Length - 4, 4), 1);

        var reader = new StateReader(state);

        Assert.Throws<InvalidStateException>(() => reader.TryReadSection(out _));
    }

    [Fact]
    public void Crc32_MatchesKnownVectorAndReferenceImplementation()
    {
        Assert.Equal(0xCBF43926u, StateCrc32.Compute("123456789"u8));

        var random = new Random(12345);
        foreach (var length in new[] { 0, 1, 7, 8, 9, 31, 32, 33, 4096, 65537 })
        {
            var data = new byte[length];
            random.NextBytes(data);
            Assert.Equal(ReferenceCrc32(data), StateCrc32.Compute(data));
        }
    }

    [Fact]
    public void Reset_ClearsWrittenDataAndKeepsBuffersUsable()
    {
        var writer = new StateWriter(1024);
        writer.WriteUInt32(0xDEADBEEF);
        Assert.Equal(4, writer.Length);

        writer.Reset();
        Assert.Equal(0, writer.Length);

        writer.WriteUInt16(0x1234);
        Assert.Equal(new byte[] { 0x34, 0x12 }, writer.ToArray());
    }

    [Fact]
    public void FastCompressionLevel_RoundtripsLargeSection()
    {
        var source = new LargeTestSection { Payload = CreatePayload(64 * 1024) };
        var writer = new StateWriter { CompressionLevel = System.IO.Compression.CompressionLevel.Fastest };
        writer.WriteSection(source);
        var state = writer.ToArray();

        var reader = new StateReader(state);
        Assert.True(reader.TryReadSection(out StateSectionReader? sectionReader));
        Assert.NotNull(sectionReader);

        var restored = new LargeTestSection();
        restored.Load(sectionReader);
        Assert.Equal(source.Payload.ToArray(), restored.Payload.ToArray());
    }

    private static uint ReferenceCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    private static byte[] CreateIncompressiblePayload(int length)
    {
        var payload = new byte[length];
        var state = 0x12345678u;
        for (var i = 0; i < payload.Length; i++)
        {
            state = (state * 1664525u) + 1013904223u;
            payload[i] = (byte)(state >> 24);
        }

        return payload;
    }

    private static byte[] CreatePayload(int length)
    {
        var payload = new byte[length];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 251);
        }

        return payload;
    }

    private sealed class LargeTestSection : IStateSection
    {
        private const uint SectionId = 0x87654321;

        public uint Id => SectionId;

        public ushort Version => 1;

        public ReadOnlyMemory<byte> Payload { get; set; }

        public void Save(IStateWriter writer)
        {
            writer.WriteBytes(Payload.Span);
        }

        public void Load(IStateReader reader)
        {
            var payload = new byte[reader.Remaining];
            reader.ReadBytes(payload);
            Payload = payload;
        }
    }

    private sealed class TestStateSection : IStateSection
    {
        public const uint SectionId = 0x12345678;
        public const ushort SectionVersion = 3;

        public uint Id { get; init; } = SectionId;

        public ushort Version => SectionVersion;

        public int Value { get; set; }

        public string Text { get; set; } = string.Empty;

        public void Save(IStateWriter writer)
        {
            writer.WriteInt32(Value);
            writer.WriteString(Text);
        }

        public void Load(IStateReader reader)
        {
            Value = reader.ReadInt32();
            Text = reader.ReadString();
        }
    }
}
