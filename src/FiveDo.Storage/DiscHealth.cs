using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>One disc track compared against a DAT.</summary>
/// <param name="Track">Track number, or 1 for an untracked source.</param>
/// <param name="Size">Native bytes hashed.</param>
/// <param name="Crc32">Uppercase CRC-32 of the native bytes.</param>
/// <param name="Md5">Uppercase MD5 of the native bytes.</param>
/// <param name="Sha1">Uppercase SHA-1 of the native bytes.</param>
/// <param name="Matched">Whether a DAT ROM entry matched size and every supplied hash.</param>
/// <param name="Game">Matched DAT game name, or empty.</param>
/// <param name="Rom">Matched DAT ROM name, or empty.</param>
public sealed record DiscDatTrackResult(
    int Track,
    long Size,
    string Crc32,
    string Md5,
    string Sha1,
    bool Matched,
    string Game,
    string Rom);

/// <summary>Result of matching a disc against a Redump/No-Intro DAT.</summary>
/// <param name="KnownDump">Whether every track matched a DAT entry.</param>
/// <param name="GameName">Matched game name when all tracks agree, otherwise empty.</param>
/// <param name="Tracks">Per-track results.</param>
/// <param name="MatchedTracks">Number of matching tracks.</param>
public sealed record DiscDatResult(
    bool KnownDump,
    string GameName,
    IReadOnlyList<DiscDatTrackResult> Tracks,
    int MatchedTracks);

/// <summary>
/// Hashes a disc image's native track bytes and matches them against a DAT file.
/// </summary>
/// <remarks>
/// The hash covers the native track payloads in logical order (2352 bytes for raw data
/// and audio, 2048 for cooked data), which is the representation Redump/No-Intro DATs
/// use for a raw <c>.bin</c>. CHD images are decompressed, so a CHD file hash will not
/// match; this verifier checks the reconstructed disc content instead.
/// </remarks>
public static class DiscHealth
{
    /// <summary>Matches a disc against a DAT.</summary>
    /// <param name="disc">Disc source. The verifier does not dispose it.</param>
    /// <param name="dat">Parsed DAT.</param>
    /// <returns>The per-track and overall match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static DiscDatResult Verify(IDiscSource disc, RedumpDat dat)
    {
        ArgumentNullException.ThrowIfNull(disc);
        ArgumentNullException.ThrowIfNull(dat);

        var tracks = new List<DiscDatTrackResult>();
        if (disc is ITrackedDiscSource tracked)
        {
            foreach (DiscTrack track in tracked.Tracks)
            {
                tracks.Add(HashTrack(
                    tracked,
                    track.Number,
                    track.StartLba,
                    track.SectorCount,
                    Math.Max(1, track.SectorSize),
                    dat));
            }
        }
        else
        {
            tracks.Add(HashUntracked(disc, dat));
        }

        var matched = tracks.Count(result => result.Matched);
        var known = tracks.Count > 0 && matched == tracks.Count;
        var gameName = string.Empty;
        if (known)
        {
            foreach (DiscDatTrackResult track in tracks)
            {
                if (gameName.Length == 0)
                {
                    gameName = track.Game;
                }
                else if (!string.Equals(gameName, track.Game, StringComparison.Ordinal))
                {
                    gameName = string.Empty;
                    break;
                }
            }
        }

        return new DiscDatResult(known, gameName, tracks, matched);
    }

    private static DiscDatTrackResult HashTrack(
        ITrackedDiscSource disc,
        int trackNumber,
        int startLba,
        int sectorCount,
        int sectorSize,
        RedumpDat dat)
    {
        var buffer = new byte[sectorSize];
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using IncrementalHash sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var crc = 0xFFFFFFFFu;
        long total = 0;
        var sectorsPerChunk = Math.Max(1, 65536 / sectorSize);
        for (var i = 0; i < sectorCount; i += sectorsPerChunk)
        {
            var end = Math.Min(sectorCount, i + sectorsPerChunk);
            for (var sector = i; sector < end; sector++)
            {
                int read = disc.ReadTrackSector(startLba + sector, buffer);
                if (read <= 0)
                {
                    continue;
                }

                ReadOnlySpan<byte> data = buffer.AsSpan(0, Math.Min(read, buffer.Length));
                crc = Crc32.Append(crc, data);
                md5.AppendData(data);
                sha1.AppendData(data);
                total += data.Length;
            }
        }

        return Match(
            trackNumber,
            total,
            ~crc,
            md5.GetHashAndReset(),
            sha1.GetHashAndReset(),
            dat);
    }

    private static DiscDatTrackResult HashUntracked(IDiscSource disc, RedumpDat dat)
    {
        var buffer = new byte[2048];
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using IncrementalHash sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var crc = 0xFFFFFFFFu;
        long total = 0;
        for (var lba = 0; lba < disc.SectorCount; lba++)
        {
            disc.ReadSector(lba, buffer);
            ReadOnlySpan<byte> data = buffer.AsSpan(0, Math.Min(2048, buffer.Length));
            crc = Crc32.Append(crc, data);
            md5.AppendData(data);
            sha1.AppendData(data);
            total += data.Length;
        }

        return Match(1, total, ~crc, md5.GetHashAndReset(), sha1.GetHashAndReset(), dat);
    }

    private static DiscDatTrackResult Match(
        int track,
        long size,
        uint crc,
        byte[] md5,
        byte[] sha1,
        RedumpDat dat)
    {
        var crcText = crc.ToString("X8", CultureInfo.InvariantCulture);
        var md5Text = Convert.ToHexString(md5);
        var sha1Text = Convert.ToHexString(sha1);
        foreach (DatGame game in dat.Games)
        {
            foreach (DatRom rom in game.Roms)
            {
                if (rom.Size != size)
                {
                    continue;
                }

                if (rom.Crc32.Length > 0 && !string.Equals(rom.Crc32, crcText, StringComparison.Ordinal))
                {
                    continue;
                }

                if (rom.Md5.Length > 0 && !string.Equals(rom.Md5, md5Text, StringComparison.Ordinal))
                {
                    continue;
                }

                if (rom.Sha1.Length > 0 && !string.Equals(rom.Sha1, sha1Text, StringComparison.Ordinal))
                {
                    continue;
                }

                return new DiscDatTrackResult(track, size, crcText, md5Text, sha1Text, true, game.Name, rom.Name);
            }
        }

        return new DiscDatTrackResult(track, size, crcText, md5Text, sha1Text, false, string.Empty, string.Empty);
    }

    private static class Crc32
    {
        private static readonly uint[] Table = CreateTable();

        internal static uint Append(uint crc, ReadOnlySpan<byte> data)
        {
            foreach (var value in data)
            {
                crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return crc;
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
