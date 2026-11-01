using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Tests.TestUtilities;

/// <summary>Builds legacy-format states for backward-compatibility tests.</summary>
internal static class StateTestHelper
{
    /// <summary>
    /// Rewrites a v3 state as a v2 or v1 state with the same header fields and sections.
    /// </summary>
    /// <param name="state">A v3 state written by the current build.</param>
    /// <param name="version">Target version (1 or 2).</param>
    /// <returns>The downgraded state bytes.</returns>
    internal static byte[] DowngradeToVersion(byte[] state, ushort version)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (version is not 1 and not 2)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        using var input = new MemoryStream(state);
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        uint magic = reader.ReadUInt32();
        ushort actualVersion = reader.ReadUInt16();
        if (actualVersion != StateFormat.Version)
        {
            throw new InvalidDataException($"Expected a v{StateFormat.Version} state, got v{actualVersion}.");
        }

        ushort headerFlags = reader.ReadUInt16();
        var emulatorVersion = ReadString(reader);
        int armClock = reader.ReadInt32();
        uint fixMode = reader.ReadUInt32();
        var biosHash = ReadString(reader);
        var discHash = ReadString(reader);
        long frames = reader.ReadInt64();
        if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
        {
            _ = reader.ReadUInt32();
        }

        if ((headerFlags & StateFormat.HeaderFlagSectionTable) != 0)
        {
            uint count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt16();
                _ = reader.ReadUInt16();
            }
        }

        long sectionOffset = input.Position;
        var writer = new StateWriter();
        writer.WriteUInt32(magic);
        writer.WriteUInt16(version);
        writer.WriteUInt16(0);
        writer.WriteString(emulatorVersion);
        writer.WriteInt32(armClock);
        writer.WriteUInt32(fixMode);
        writer.WriteString(biosHash);
        writer.WriteString(discHash);
        writer.WriteInt64(frames);
        writer.WriteBytes(state.AsSpan((int)sectionOffset));
        return writer.ToArray();
    }

    /// <summary>
    /// Declares and appends an extra section to a v3 state so unknown-section policy can be tested.
    /// </summary>
    /// <param name="state">A v3 state written by the current build.</param>
    /// <param name="section">Section to append.</param>
    /// <returns>A state whose table declares the section and whose payload follows the known sections.</returns>
    internal static byte[] AppendDeclaredSection(byte[] state, IStateSection section)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(section);
        using var input = new MemoryStream(state);
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        _ = reader.ReadUInt32();
        ushort version = reader.ReadUInt16();
        ushort headerFlags = reader.ReadUInt16();
        if (version != StateFormat.Version
            || (headerFlags & StateFormat.HeaderFlagSectionTable) == 0)
        {
            throw new InvalidDataException("Expected a v3 state with a section table.");
        }

        _ = ReadString(reader);
        _ = reader.ReadInt32();
        _ = reader.ReadUInt32();
        _ = ReadString(reader);
        _ = ReadString(reader);
        _ = reader.ReadInt64();
        if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
        {
            _ = reader.ReadUInt32();
        }

        long countOffset = input.Position;
        uint count = reader.ReadUInt32();
        long tableEnd = countOffset + 4 + (count * 8L);

        var sectionWriter = new StateWriter();
        sectionWriter.WriteSection(section);
        byte[] result = new byte[state.Length + 8 + sectionWriter.Length];
        int offset = 0;
        state.AsSpan(0, (int)countOffset).CopyTo(result);
        offset += (int)countOffset;
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), count + 1);
        offset += 4;
        state.AsSpan((int)countOffset + 4, (int)(tableEnd - countOffset - 4)).CopyTo(result.AsSpan(offset));
        offset += (int)(tableEnd - countOffset - 4);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), section.Id);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 4), section.Version);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset + 6), 0);
        offset += 8;
        state.AsSpan((int)tableEnd).CopyTo(result.AsSpan(offset));
        offset += state.Length - (int)tableEnd;
        sectionWriter.WrittenSpan.CopyTo(result.AsSpan(offset));
        return result;
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0)
        {
            throw new InvalidDataException("Negative string length.");
        }

        byte[] bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidDataException("Truncated string.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
