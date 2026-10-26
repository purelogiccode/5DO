using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>One section in a save-state file.</summary>
/// <param name="Id">Section identifier.</param>
/// <param name="Version">Section format version.</param>
/// <param name="PayloadLength">Payload length in bytes.</param>
public sealed record StateSectionInfo(uint Id, ushort Version, int PayloadLength);

/// <summary>The header and section table of a save-state file.</summary>
/// <param name="Version">State format version.</param>
/// <param name="EmulatorVersion">Writer's emulator version.</param>
/// <param name="ArmClock">Recorded ARM clock.</param>
/// <param name="FixMode">Recorded fix-mode flags.</param>
/// <param name="BiosHash">Recorded BIOS MD5, or empty.</param>
/// <param name="DiscHash">Recorded disc hash, or empty.</param>
/// <param name="FramesExecuted">Recorded frame count.</param>
/// <param name="Sections">Section table.</param>
public sealed record StateInfo(
    ushort Version,
    string EmulatorVersion,
    int ArmClock,
    uint FixMode,
    string BiosHash,
    string DiscHash,
    long FramesExecuted,
    IReadOnlyList<StateSectionInfo> Sections);

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
        using var stream = File.OpenRead(path);
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
        var reader = new StateReader(buffer.ToArray());
        if (reader.Remaining < 8)
        {
            throw new InvalidStateException("The save state is truncated.");
        }

        if (reader.ReadUInt32() != StateFormat.Magic)
        {
            throw new InvalidStateException("The save state has an invalid magic value.");
        }

        var version = reader.ReadUInt16();
        if (version != StateFormat.Version)
        {
            throw new InvalidStateException($"Unsupported save-state version {version}.");
        }

        _ = reader.ReadUInt16();
        var emulatorVersion = reader.ReadString();
        var armClock = reader.ReadInt32();
        var fixMode = reader.ReadUInt32();
        var biosHash = reader.ReadString();
        var discHash = reader.ReadString();
        var frames = reader.ReadInt64();

        var sections = new List<StateSectionInfo>();
        while (reader.TryReadSection(out var section))
        {
            sections.Add(new StateSectionInfo(section.Id, section.Version, section.PayloadLength));
        }

        return new StateInfo(version, emulatorVersion, armClock, fixMode, biosHash, discHash, frames, sections);
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
        using var stream = File.OpenRead(path);
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
        if (version != StateFormat.Version)
        {
            throw new InvalidStateException($"Unsupported save-state version {version}.");
        }

        _ = reader.ReadUInt16();
        var emulatorVersion = ReadString(reader);
        var armClock = reader.ReadInt32();
        var fixMode = reader.ReadUInt32();
        var biosHash = ReadString(reader);
        var discHash = ReadString(reader);
        var frames = reader.ReadInt64();

        var sections = new List<StateSectionInfo>();
        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < StateFormat.SectionHeaderSize)
            {
                throw new InvalidStateException("The save state is truncated in the section table.");
            }

            var id = reader.ReadUInt32();
            var sectionVersion = reader.ReadUInt16();
            _ = reader.ReadUInt16();
            var length = reader.ReadUInt32();
            if (length > int.MaxValue)
            {
                throw new InvalidStateException($"Section 0x{id:X8} has an invalid payload length.");
            }

            if (stream.Length - stream.Position < length + StateFormat.SectionCrcSize)
            {
                throw new InvalidStateException($"Section 0x{id:X8} is truncated.");
            }

            stream.Seek(length + StateFormat.SectionCrcSize, SeekOrigin.Current);
            sections.Add(new StateSectionInfo(id, sectionVersion, (int)length));
        }

        return new StateInfo(version, emulatorVersion, armClock, fixMode, biosHash, discHash, frames, sections);
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
