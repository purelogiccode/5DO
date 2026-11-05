using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>
/// Reads a save-state header and section table without loading the emulator.
/// </summary>
public static class StateInspector
{
    /// <summary>Reads a state file.</summary>
    /// <param name="path">State file path.</param>
    /// <returns>The parsed header and section table.</returns>
    /// <exception cref="InvalidStateException">Thrown when the file is not a valid state.</exception>
    public static StateInfo Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads a state stream.</summary>
    /// <param name="stream">State stream.</param>
    /// <returns>The parsed header and section table.</returns>
    /// <exception cref="InvalidStateException">Thrown when the state is invalid.</exception>
    public static StateInfo Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var reader = new StateReader(bytes);
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
        if (version >= 3)
        {
            if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
            {
                _ = reader.ReadUInt32();
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

        var sections = new List<StateSectionInfo>();
        var compressed = false;
        while (reader.TryReadSection(out StateSectionReader? section))
        {
            sections.Add(new StateSectionInfo(
                section.Id,
                section.Version,
                section.PayloadLength,
                section.Compressed,
                section.StoredLength));
            compressed |= section.Compressed;
        }

        return new StateInfo(
            version,
            emulatorVersion,
            armClock,
            fixMode,
            biosHash,
            discHash,
            frames,
            sections,
            compressed,
            bytes.Length);
    }

    /// <summary>
    /// Reads only the header and section table without loading section payloads.
    /// </summary>
    /// <remarks>
    /// This is the fast path for save-slot metadata: it seeks over the payloads and does not
    /// validate their CRCs. Use <see cref="Read(Stream)"/> when full validation is required.
    /// </remarks>
    /// <param name="path">State file path.</param>
    /// <returns>The parsed header and section table.</returns>
    /// <exception cref="InvalidStateException">Thrown when the header or section table is invalid.</exception>
    public static StateInfo ReadHeader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        return ReadHeader(stream);
    }

    /// <summary>Reads only the header and section table from a seekable stream.</summary>
    /// <param name="stream">Seekable state stream.</param>
    /// <returns>The parsed header and section table.</returns>
    /// <exception cref="InvalidStateException">Thrown when the header or section table is invalid.</exception>
    public static StateInfo ReadHeader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            return Read(stream);
        }

        var startPosition = stream.Position;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length - stream.Position < 8)
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
        var emulatorVersion = ReadString(reader);
        var armClock = reader.ReadInt32();
        var fixMode = reader.ReadUInt32();
        var biosHash = ReadString(reader);
        var discHash = ReadString(reader);
        var frames = reader.ReadInt64();
        if (version >= 3)
        {
            if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
            {
                _ = reader.ReadUInt32();
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

        var sections = new List<StateSectionInfo>();
        var compressed = false;
        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < StateFormat.SectionHeaderSize)
            {
                throw new InvalidStateException("The save state is truncated in the section table.");
            }

            var id = reader.ReadUInt32();
            var sectionVersion = reader.ReadUInt16();
            var flags = reader.ReadUInt16();
            var length = reader.ReadUInt32();
            if (length > int.MaxValue)
            {
                throw new InvalidStateException($"Section 0x{id:X8} has an invalid payload length.");
            }

            if ((flags & ~(StateFormat.SectionFlagCompressed | StateFormat.SectionFlagUnchecked)) != 0)
            {
                throw new InvalidStateException($"Section 0x{id:X8} has unsupported flags 0x{flags:X4}.");
            }

            var storedLength = (int)length;
            if (stream.Length - stream.Position < storedLength + StateFormat.SectionCrcSize)
            {
                throw new InvalidStateException($"Section 0x{id:X8} is truncated.");
            }

            var sectionCompressed = (flags & StateFormat.SectionFlagCompressed) != 0;
            int payloadLength = storedLength;
            if (sectionCompressed)
            {
                if (storedLength < StateFormat.CompressedSectionPrefixSize)
                {
                    throw new InvalidStateException($"Section 0x{id:X8} has a truncated compressed payload.");
                }

                payloadLength = reader.ReadInt32();
                if (payloadLength < 0 || payloadLength > StateFormat.MaxUncompressedSectionLength)
                {
                    throw new InvalidStateException(
                        $"Section 0x{id:X8} has an invalid uncompressed length {payloadLength}.");
                }

                stream.Seek(
                    storedLength - StateFormat.CompressedSectionPrefixSize + StateFormat.SectionCrcSize,
                    SeekOrigin.Current);
            }
            else
            {
                stream.Seek(storedLength + StateFormat.SectionCrcSize, SeekOrigin.Current);
            }

            sections.Add(new StateSectionInfo(id, sectionVersion, payloadLength, sectionCompressed, storedLength));
            compressed |= sectionCompressed;
        }

        return new StateInfo(
            version,
            emulatorVersion,
            armClock,
            fixMode,
            biosHash,
            discHash,
            frames,
            sections,
            compressed,
            stream.Length - startPosition);
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length < 0)
        {
            throw new InvalidStateException("A state string has a negative length.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidStateException("The save state is truncated in a header string.");
        }

        try
        {
            return new System.Text.UTF8Encoding(false, true).GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException exception)
        {
            throw new InvalidStateException("A state string is not valid UTF-8.", exception);
        }
    }
}
