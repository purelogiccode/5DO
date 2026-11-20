using System;
using FiveDo.Core;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers parsing, section editing, and round-tripping of 5DO save-state documents.
/// </summary>
public sealed class FiveDoStateDocumentTests
{
    private const uint FirstSectionId = 0x11111111;
    private const uint SecondSectionId = 0x22222222;

    [Fact]
    public void Parse_ReadsTheHeaderAndSections()
    {
        byte[] state = CreateState(("1.0.0", 60, 0x1234, "bios-hash", "disc-hash", 100L));

        FiveDoStateDocument document = FiveDoStateDocument.Parse(state);

        Assert.Equal("1.0.0", document.EmulatorVersion);
        Assert.Equal(60, document.ArmClock);
        Assert.Equal(0x1234u, document.FixMode);
        Assert.Equal("bios-hash", document.BiosHash);
        Assert.Equal("disc-hash", document.DiscHash);
        Assert.Equal(100L, document.Frames);
        Assert.Equal(2, document.Sections.Count);
        Assert.Equal(FirstSectionId, document.Sections[0].Id);
        Assert.Equal(SecondSectionId, document.Sections[1].Id);
        Assert.Equal([1, 2, 3], document.Sections[0].Payload);
    }

    [Fact]
    public void Find_ReturnsTheMatchingSectionOrNull()
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(CreateState());

        Assert.NotNull(document.Find(FirstSectionId));
        Assert.Equal(SecondSectionId, document.Find(SecondSectionId)!.Id);
        Assert.Null(document.Find(0xDEADBEEF));
    }

    [Fact]
    public void Replace_ReplacesAnExistingSection()
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(CreateState());
        byte[] replacement = [9, 8, 7, 6];

        document.Replace(FirstSectionId, 3, replacement);

        FiveDoStateSection section = document.Find(FirstSectionId)!;
        Assert.Equal((ushort)3, section.Version);
        Assert.Equal(replacement, section.Payload);
        Assert.Equal(2, document.Sections.Count);
    }

    [Fact]
    public void Replace_AppendsANewSection()
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(CreateState());
        byte[] payload = [4, 5, 6];

        document.Replace(0x33333333, 1, payload);

        Assert.Equal(3, document.Sections.Count);
        Assert.Equal(payload, document.Find(0x33333333)!.Payload);
    }

    [Fact]
    public void Remove_DeletesTheSectionAndReportsWhetherItExisted()
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(CreateState());

        Assert.True(document.Remove(FirstSectionId));
        Assert.Null(document.Find(FirstSectionId));
        Assert.False(document.Remove(FirstSectionId));
        Assert.Single(document.Sections);
    }

    [Fact]
    public void Write_RoundTripsEveryEditedField()
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(CreateState());
        document.Replace(FirstSectionId, 2, [7, 7, 7]);
        document.Remove(SecondSectionId);

        byte[] written = document.Write(compress: false);
        FiveDoStateDocument reparsed = FiveDoStateDocument.Parse(written);

        Assert.Equal(document.EmulatorVersion, reparsed.EmulatorVersion);
        Assert.Equal(document.ArmClock, reparsed.ArmClock);
        Assert.Equal(document.FixMode, reparsed.FixMode);
        Assert.Equal(document.BiosHash, reparsed.BiosHash);
        Assert.Equal(document.DiscHash, reparsed.DiscHash);
        Assert.Equal(document.Frames, reparsed.Frames);
        Assert.Single(reparsed.Sections);
        Assert.Equal([7, 7, 7], reparsed.Sections[0].Payload);
    }

    [Fact]
    public void Write_CompressedStateCanBeReparsed()
    {
        FiveDoStateDocument document = FiveDoStateDocument.Parse(CreateState());

        byte[] written = document.Write(compress: true);
        FiveDoStateDocument reparsed = FiveDoStateDocument.Parse(written);

        Assert.Equal(document.Sections.Count, reparsed.Sections.Count);
        Assert.Equal(document.Sections[0].Payload, reparsed.Sections[0].Payload);
    }

    [Fact]
    public void Parse_RejectsATruncatedDocument()
    {
        Assert.Throws<InvalidStateException>(() => FiveDoStateDocument.Parse([]));
    }

    [Fact]
    public void Parse_RejectsAnInvalidMagicValue()
    {
        byte[] state = CreateState();
        state[0] = 0;

        Assert.Throws<InvalidStateException>(() => FiveDoStateDocument.Parse(state));
    }

    private static byte[] CreateState(
        (string EmulatorVersion, int ArmClock, uint FixMode, string BiosHash, string DiscHash, long Frames)? header = null)
    {
        (string emulatorVersion, int armClock, uint fixMode, string biosHash, string discHash, long frames) =
            header ?? ("1.0.0", 60, 0x1234, "bios-hash", "disc-hash", 100L);

        var writer = new StateWriter();
        writer.WriteUInt32(StateFormat.Magic);
        writer.WriteUInt16(StateFormat.Version);
        writer.WriteUInt16(StateFormat.HeaderFlagFeatureFlags | StateFormat.HeaderFlagSectionTable);
        writer.WriteString(emulatorVersion);
        writer.WriteInt32(armClock);
        writer.WriteUInt32(fixMode);
        writer.WriteString(biosHash);
        writer.WriteString(discHash);
        writer.WriteInt64(frames);
        writer.WriteUInt32(StateFormat.FeatureSectionTable);
        writer.WriteUInt32(2);
        writer.WriteUInt32(FirstSectionId);
        writer.WriteUInt16(1);
        writer.WriteUInt16(0);
        writer.WriteUInt32(SecondSectionId);
        writer.WriteUInt16(1);
        writer.WriteUInt16(0);
        writer.WriteSection(new TestSection(FirstSectionId, 1, [1, 2, 3]));
        writer.WriteSection(new TestSection(SecondSectionId, 1, [4, 5]));
        return writer.ToArray();
    }

    private sealed class TestSection(uint id, ushort version, byte[] payload) : IStateSection
    {
        public uint Id { get; } = id;

        public ushort Version { get; } = version;

        public void Save(IStateWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            foreach (var value in payload)
            {
                writer.WriteByte(value);
            }
        }

        public void Load(IStateReader reader)
        {
            throw new NotSupportedException("Test sections are write-only.");
        }
    }
}
