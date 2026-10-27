using System;
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
