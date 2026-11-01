using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>One section compared between two states.</summary>
/// <param name="Id">Section identifier.</param>
/// <param name="Name">Human-readable section name.</param>
/// <param name="VersionA">Version in the first state, or 0 when absent.</param>
/// <param name="VersionB">Version in the second state, or 0 when absent.</param>
/// <param name="LengthA">Uncompressed payload length in the first state.</param>
/// <param name="LengthB">Uncompressed payload length in the second state.</param>
/// <param name="HashA">FNV-1a hash of the first payload.</param>
/// <param name="HashB">FNV-1a hash of the second payload.</param>
/// <param name="FirstDifference">First differing byte offset, or -1 when equal.</param>
/// <param name="DifferingBytes">Number of differing bytes in the overlapping range.</param>
public sealed record StateSectionDiff(
    uint Id,
    string Name,
    ushort VersionA,
    ushort VersionB,
    int LengthA,
    int LengthB,
    uint HashA,
    uint HashB,
    long FirstDifference,
    int DifferingBytes)
{
    /// <summary>Gets a value indicating whether the section is identical in both states.</summary>
    public bool IsSame =>
        VersionA == VersionB
        && LengthA == LengthB
        && HashA == HashB
        && FirstDifference < 0;
}

/// <summary>The result of comparing two save states.</summary>
/// <param name="VersionA">Format version of the first state.</param>
/// <param name="VersionB">Format version of the second state.</param>
/// <param name="FramesA">Frame count from the first state.</param>
/// <param name="FramesB">Frame count from the second state.</param>
/// <param name="DiscHashA">Disc hash from the first state, or empty.</param>
/// <param name="DiscHashB">Disc hash from the second state, or empty.</param>
/// <param name="Sections">Per-section comparison, sorted by identifier.</param>
public sealed record StateDiffResult(
    ushort VersionA,
    ushort VersionB,
    long FramesA,
    long FramesB,
    string DiscHashA,
    string DiscHashB,
    IReadOnlyList<StateSectionDiff> Sections)
{
    /// <summary>Gets the number of sections that differ or exist in only one state.</summary>
    public int ChangedSections
    {
        get
        {
            var changed = 0;
            foreach (StateSectionDiff section in Sections)
            {
                if (!section.IsSame)
                {
                    changed++;
                }
            }

            return changed;
        }
    }
}

/// <summary>
/// Compares two save states section by section without loading either into a machine.
/// </summary>
/// <remarks>
/// Payloads are decompressed so CPU/register/memory differences are visible even when
/// the writer chose different compression ratios. Hashes are FNV-1a and are diagnostic
/// only; the state's own CRC validation still runs while reading.
/// </remarks>
public static class StateDiff
{
    /// <summary>Compares two state files.</summary>
    /// <param name="firstPath">First state path.</param>
    /// <param name="secondPath">Second state path.</param>
    /// <returns>The comparison result.</returns>
    public static StateDiffResult Compare(string firstPath, string secondPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondPath);
        return Compare(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
    }

    /// <summary>Compares two state byte arrays.</summary>
    /// <param name="first">First state bytes.</param>
    /// <param name="second">Second state bytes.</param>
    /// <returns>The comparison result.</returns>
    /// <exception cref="InvalidStateException">Thrown when either state is malformed.</exception>
    public static StateDiffResult Compare(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        ParsedState a = Parse(first);
        ParsedState b = Parse(second);
        var ids = new SortedSet<uint>(a.Sections.Keys);
        ids.UnionWith(b.Sections.Keys);
        var sections = new List<StateSectionDiff>(ids.Count);
        foreach (var id in ids)
        {
            a.Sections.TryGetValue(id, out ParsedSection? left);
            b.Sections.TryGetValue(id, out ParsedSection? right);
            byte[] leftPayload = left?.Payload ?? [];
            byte[] rightPayload = right?.Payload ?? [];
            (long firstDifference, int differingBytes) = CompareBytes(leftPayload, rightPayload);
            sections.Add(new StateSectionDiff(
                id,
                StateSectionIds.GetName(id),
                left?.Version ?? 0,
                right?.Version ?? 0,
                left?.Payload.Length ?? 0,
                right?.Payload.Length ?? 0,
                Hash(leftPayload),
                Hash(rightPayload),
                firstDifference,
                differingBytes));
        }

        return new StateDiffResult(
            a.Version,
            b.Version,
            a.Frames,
            b.Frames,
            a.DiscHash,
            b.DiscHash,
            sections);
    }

    private static ParsedState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new StateReader(data.ToArray());
        if (reader.Remaining < 8 || reader.ReadUInt32() != StateFormat.Magic)
        {
            throw new InvalidStateException("The save state has an invalid magic value.");
        }

        var version = reader.ReadUInt16();
        if (!StateFormat.IsSupportedVersion(version))
        {
            throw new InvalidStateException($"Unsupported save-state version {version}.");
        }

        var headerFlags = reader.ReadUInt16();
        _ = reader.ReadString();
        _ = reader.ReadInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadString();
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

        var sections = new Dictionary<uint, ParsedSection>();
        while (reader.TryReadSection(out StateSectionReader? section))
        {
            var payload = new byte[section.PayloadLength];
            section.ReadBytes(payload);
            if (!sections.TryAdd(section.Id, new ParsedSection(section.Version, payload)))
            {
                throw new InvalidStateException($"Section 0x{section.Id:X8} appears twice in the state.");
            }
        }

        return new ParsedState(version, frames, discHash, sections);
    }

    private static (long FirstDifference, int DifferingBytes) CompareBytes(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second)
    {
        int common = Math.Min(first.Length, second.Length);
        long firstDifference = -1;
        var differing = 0;
        for (var i = 0; i < common; i++)
        {
            if (first[i] == second[i])
            {
                continue;
            }

            if (firstDifference < 0)
            {
                firstDifference = i;
            }

            differing++;
        }

        if (firstDifference < 0 && first.Length != second.Length)
        {
            firstDifference = common;
        }

        return (firstDifference, differing);
    }

    private static uint Hash(ReadOnlySpan<byte> data)
    {
        var hash = 2166136261u;
        foreach (var value in data)
        {
            hash = unchecked((hash ^ value) * 16777619u);
        }

        return hash;
    }

    private sealed record ParsedSection(ushort Version, byte[] Payload);

    private sealed record ParsedState(
        ushort Version,
        long Frames,
        string DiscHash,
        Dictionary<uint, ParsedSection> Sections);
}
