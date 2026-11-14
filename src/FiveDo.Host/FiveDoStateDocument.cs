using System;
using System.Collections.Generic;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// A parsed 5DO save-state file that can replace individual section payloads and be
/// written back without loading the emulator.
/// </summary>
/// <remarks>
/// This is the host-side bridge used by the 4DO state importer. It preserves every
/// section it does not explicitly replace, including the derived video frame and the
/// CD-ROM drive state.
/// </remarks>
internal sealed class FiveDoStateDocument
{
    private FiveDoStateDocument(
        string emulatorVersion,
        int armClock,
        uint fixMode,
        string biosHash,
        string discHash,
        long frames,
        uint features,
        List<FiveDoStateSection> sections)
    {
        EmulatorVersion = emulatorVersion;
        ArmClock = armClock;
        FixMode = fixMode;
        BiosHash = biosHash;
        DiscHash = discHash;
        Frames = frames;
        Features = features;
        Sections = sections;
    }

    public string EmulatorVersion { get; }

    public int ArmClock { get; }

    public uint FixMode { get; }

    public string BiosHash { get; }

    public string DiscHash { get; }

    public long Frames { get; }

    public uint Features { get; private set; }

    public List<FiveDoStateSection> Sections { get; }

    public static FiveDoStateDocument Parse(ReadOnlySpan<byte> data)
    {
        var reader = new StateReader(data.ToArray());
        if (reader.Remaining < 8)
        {
            throw new InvalidStateException("The save state is truncated.");
        }

        if (reader.ReadUInt32() != StateFormat.Magic)
        {
            throw new InvalidStateException("The save state has an invalid magic value.");
        }

        var version = reader.ReadUInt16();
        if (!StateFormat.IsSupportedVersion(version))
        {
            throw new InvalidStateException($"Unsupported save-state version {version}.");
        }

        var headerFlags = reader.ReadUInt16();
        var emulatorVersion = reader.ReadString();
        var armClock = reader.ReadInt32();
        var fixMode = reader.ReadUInt32();
        var biosHash = reader.ReadString();
        var discHash = reader.ReadString();
        var frames = reader.ReadInt64();
        var features = 0u;
        if (version >= 3)
        {
            if ((headerFlags & ~StateFormat.HeaderFlagAll) != 0)
            {
                throw new InvalidStateException($"State header has unknown flags 0x{headerFlags:X4}.");
            }

            if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
            {
                features = reader.ReadUInt32();
                if ((features & ~StateFormat.FeatureAll) != 0)
                {
                    throw new InvalidStateException(
                        $"State declares unknown features 0x{features & ~StateFormat.FeatureAll:X8}.");
                }
            }

            if ((headerFlags & StateFormat.HeaderFlagSectionTable) != 0)
            {
                var count = reader.ReadUInt32();
                if (count > StateFormat.MaxSectionTableEntries)
                {
                    throw new InvalidStateException($"The state declares {count} sections, which is too many.");
                }

                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();
                }
            }
        }

        var sections = new List<FiveDoStateSection>();
        var ids = new HashSet<uint>();
        while (reader.TryReadSection(out StateSectionReader? section))
        {
            if (!ids.Add(section.Id))
            {
                throw new InvalidStateException($"Section 0x{section.Id:X8} appears twice.");
            }

            var payload = new byte[section.PayloadLength];
            section.ReadBytes(payload);
            if (section.Remaining != 0)
            {
                throw new InvalidStateException(
                    $"Section 0x{section.Id:X8} has {section.Remaining} trailing payload bytes.");
            }

            sections.Add(new FiveDoStateSection(section.Id, section.Version, payload));
        }

        if (sections.Count == 0)
        {
            throw new InvalidStateException("The save state contains no sections.");
        }

        return new FiveDoStateDocument(
            emulatorVersion,
            armClock,
            fixMode,
            biosHash,
            discHash,
            frames,
            features,
            sections);
    }

    public byte[] Write(bool compress = true)
    {
        var writer = new StateWriter();
        writer.CompressionEnabled = compress;
        writer.WriteUInt32(StateFormat.Magic);
        writer.WriteUInt16(StateFormat.Version);
        writer.WriteUInt16(StateFormat.HeaderFlagFeatureFlags | StateFormat.HeaderFlagSectionTable);
        writer.WriteString(EmulatorVersion);
        writer.WriteInt32(ArmClock);
        writer.WriteUInt32(FixMode);
        writer.WriteString(BiosHash);
        writer.WriteString(DiscHash);
        writer.WriteInt64(Frames);
        Features &= ~(StateFormat.FeatureSectionTable
            | StateFormat.FeatureCompressedSections
            | StateFormat.FeatureUncheckedSections);
        Features |= StateFormat.FeatureSectionTable;
        if (compress)
        {
            Features |= StateFormat.FeatureCompressedSections;
        }

        writer.WriteUInt32(Features);
        writer.WriteUInt32((uint)Sections.Count);
        foreach (FiveDoStateSection section in Sections)
        {
            writer.WriteUInt32(section.Id);
            writer.WriteUInt16(section.Version);
            writer.WriteUInt16(0);
        }

        foreach (FiveDoStateSection section in Sections)
        {
            writer.WriteSection(new RawStateSection(section.Id, section.Version, section.Payload));
        }

        return writer.ToArray();
    }

    public FiveDoStateSection? Find(uint id)
    {
        foreach (FiveDoStateSection section in Sections)
        {
            if (section.Id == id)
            {
                return section;
            }
        }

        return null;
    }

    public void Replace(uint id, ushort version, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        for (var i = 0; i < Sections.Count; i++)
        {
            if (Sections[i].Id == id)
            {
                Sections[i] = new FiveDoStateSection(id, version, payload);
                return;
            }
        }

        Sections.Add(new FiveDoStateSection(id, version, payload));
    }

    private sealed class RawStateSection(uint id, ushort version, byte[] payload) : IStateSection
    {
        public uint Id { get; } = id;

        public ushort Version { get; } = version;

        public void Save(IStateWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteBytes(payload);
        }

        public void Load(IStateReader reader)
        {
            throw new NotSupportedException("Raw state sections are write-only.");
        }
    }
}

/// <summary>One uncompressed section of a 5DO save-state file.</summary>
internal sealed record FiveDoStateSection(uint Id, ushort Version, byte[] Payload);
