using System;
using System.Collections.Generic;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// One track address reported by a host optical drive's table of contents.
/// </summary>
/// <param name="Number">One-based track number.</param>
/// <param name="IsAudio">Whether the track is Red Book CD-DA rather than data.</param>
/// <param name="StartLba">
/// Track start as a zero-based logical block address, where zero is MSF 00:02:00.
/// </param>
public readonly record struct DiscTocEntry(int Number, bool IsAudio, int StartLba);

/// <summary>
/// Translates a host optical drive's table of contents into <see cref="DiscTrack"/>
/// entries and reads raw CD-DA sectors.
/// </summary>
/// <remarks>
/// <para>
/// Data tracks are read through the host's cooked 2048-byte interface, so they are
/// reported with <see cref="DiscTrack.SectorSize"/> 2048. Audio tracks carry 2352-byte
/// Red Book frames and are read with the platform's raw-audio request:
/// <c>IOCTL_CDROM_RAW_READ</c> in CDDA mode on Windows and <c>CDROMREADAUDIO</c> on Linux.
/// </para>
/// <para>
/// The drive reports absolute MSF addresses. The 3DO CD-ROM device (and every image
/// reader in this repository) numbers logical block 0 at MSF 00:02:00, so the parser
/// subtracts the standard 150-frame lead-in.
/// </para>
/// </remarks>
public static class PhysicalDiscToc
{
    /// <summary>Gets the size of one raw Red Book audio frame.</summary>
    public const int AudioSectorBytes = 2352;

    /// <summary>Gets the size of one cooked 2048-byte data sector.</summary>
    public const int DataSectorBytes = 2048;

    private const int TrackDescriptorBytes = 8;
    private const int HeaderBytes = 4;
    private const byte ControlDataTrack = 0x04;

    /// <summary>
    /// Parses a Windows <c>CDROM_TOC</c> buffer returned by <c>IOCTL_CDROM_READ_TOC</c>.
    /// </summary>
    /// <param name="buffer">Raw TOC response.</param>
    /// <param name="fallbackLeadOutLba">
    /// Disc length used when the device does not report a lead-out descriptor. Pass -1
    /// when no other length is known.
    /// </param>
    /// <returns>The disc's tracks in disc order. Empty when the buffer is unusable.</returns>
    public static IReadOnlyList<DiscTrack> ParseWindowsToc(
        ReadOnlySpan<byte> buffer,
        int fallbackLeadOutLba = -1)
    {
        if (buffer.Length < HeaderBytes)
        {
            return [];
        }

        int declared = buffer[0] | (buffer[1] << 8);
        int length = declared >= HeaderBytes
            ? Math.Min(declared, buffer.Length)
            : buffer.Length;
        byte firstTrack = buffer[2];
        byte lastTrack = buffer[3];

        var entries = new List<DiscTocEntry>();
        var leadOut = -1;
        for (var offset = HeaderBytes; offset + TrackDescriptorBytes <= length; offset += TrackDescriptorBytes)
        {
            byte control = buffer[offset + 1];
            byte number = buffer[offset + 2];
            int lba = MsfToLba(buffer[offset + 5], buffer[offset + 6], buffer[offset + 7]);
            if (lba < 0)
            {
                continue;
            }

            // A descriptor outside the first/last track range is the lead-out. Windows
            // normally uses 0xAA, but some filter drivers leave the track byte at zero.
            if (number < firstTrack || number > lastTrack)
            {
                leadOut = Math.Max(leadOut, lba);
                continue;
            }

            entries.Add(new DiscTocEntry(number, (control & ControlDataTrack) == 0, lba));
        }

        if (leadOut <= 0)
        {
            leadOut = fallbackLeadOutLba;
        }

        return BuildTracks(entries, leadOut);
    }

    /// <summary>Builds the disc track table from parsed TOC entries and a lead-out address.</summary>
    /// <param name="entries">Track descriptors in any order.</param>
    /// <param name="leadOutLba">Exclusive end of the last track.</param>
    /// <returns>Tracks in disc order.</returns>
    public static IReadOnlyList<DiscTrack> BuildTracks(
        IReadOnlyList<DiscTocEntry> entries,
        int leadOutLba)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0 || leadOutLba <= 0)
        {
            return [];
        }

        var ordered = new List<DiscTocEntry>(entries.Count);
        var seen = new HashSet<int>();
        foreach (DiscTocEntry entry in entries)
        {
            if (entry.Number is < 1 or > 99 || entry.StartLba < 0 || !seen.Add(entry.Number))
            {
                continue;
            }

            ordered.Add(entry);
        }

        ordered.Sort(static (left, right) =>
        {
            int byAddress = left.StartLba.CompareTo(right.StartLba);
            return byAddress != 0 ? byAddress : left.Number.CompareTo(right.Number);
        });

        var tracks = new List<DiscTrack>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            int start = ordered[index].StartLba;
            int end = index + 1 < ordered.Count ? ordered[index + 1].StartLba : leadOutLba;
            if (end <= start)
            {
                continue;
            }

            DiscTocEntry entry = ordered[index];
            tracks.Add(new DiscTrack(
                entry.Number,
                entry.IsAudio ? DiscTrackKind.Audio : DiscTrackKind.Data,
                start,
                end - start,
                entry.IsAudio ? AudioSectorBytes : DataSectorBytes));
        }

        return tracks;
    }

    /// <summary>
    /// Extends a trailing data track so the table covers every sector the host's cooked
    /// interface can address.
    /// </summary>
    /// <param name="tracks">Track table from the drive's TOC.</param>
    /// <param name="cookedSectorCount">
    /// Number of 2048-byte sectors the cooked interface reports. Pass 0 when unknown.
    /// </param>
    /// <returns>
    /// The original table, or a copy whose last data track reaches
    /// <paramref name="cookedSectorCount"/>.
    /// </returns>
    /// <remarks>
    /// Some virtual drives report a lead-out address that is far short of the image they
    /// present (Windows' ISO mounter does this), and a drive's TOC can also lag a
    /// Growable image after a write. The cooked length is authoritative for data sectors,
    /// so a trailing data track is extended rather than leaving readable sectors outside
    /// the track table.
    /// </remarks>
    public static IReadOnlyList<DiscTrack> EnsureCoverage(
        IReadOnlyList<DiscTrack> tracks,
        int cookedSectorCount)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        if (tracks.Count == 0 || cookedSectorCount <= 0)
        {
            return tracks;
        }

        DiscTrack last = tracks[^1];
        if (cookedSectorCount <= last.EndLba || last.IsAudio)
        {
            return tracks;
        }

        var extended = new List<DiscTrack>(tracks);
        extended[^1] = last with { SectorCount = cookedSectorCount - last.StartLba };
        return extended;
    }

    /// <summary>Converts an MSF triplet to a zero-based logical block address.</summary>
    /// <param name="minute">Minute byte, as reported by the drive (binary, not BCD).</param>
    /// <param name="second">Second byte.</param>
    /// <param name="frame">Frame byte.</param>
    /// <returns>The logical block address, or -1 when the triplet is not a valid time.</returns>
    /// <remarks>
    /// SCSI-2 and the Windows <c>CDROM_TOC</c> layout report MSF fields as binary
    /// numbers, unlike the BCD Q-subchannel used on the media itself.
    /// </remarks>
    public static int MsfToLba(byte minute, byte second, byte frame)
    {
        if (second > 59 || frame > 74)
        {
            return -1;
        }

        return (((minute * 60) + second) * 75) + frame - 150;
    }
}
