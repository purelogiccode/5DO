using System;
using System.Collections.Generic;

namespace FiveDo.Core;

/// <summary>
/// A disc source that exposes its table of contents and native track sectors.
/// </summary>
/// <remarks>
/// <see cref="IDiscSource"/> normalizes every container to 2048-byte 3DO user-data
/// sectors. Multi-track CUE and CHD images also need the track table and raw
/// 2352-byte audio sectors for CD-DA. Implementations return native track bytes
/// from <see cref="ReadTrackSector"/>: 2048 for cooked data, 2336 for Mode 2,
/// and 2352 for raw data or audio.
/// </remarks>
public interface ITrackedDiscSource : IDiscSource
{
    /// <summary>Gets the tracks in disc order.</summary>
    IReadOnlyList<DiscTrack> Tracks { get; }

    /// <summary>Gets the total number of logical sectors across all tracks.</summary>
    int TotalSectorCount { get; }

    /// <summary>
    /// Reads one native track sector into <paramref name="destination"/>.
    /// </summary>
    /// <param name="lba">Logical block address.</param>
    /// <param name="destination">
    /// Destination span of at least the track's <see cref="DiscTrack.SectorSize"/> bytes.
    /// </param>
    /// <returns>The number of bytes written.</returns>
    int ReadTrackSector(int lba, Span<byte> destination);

    /// <summary>Finds the track that contains <paramref name="lba"/>.</summary>
    /// <param name="lba">Logical block address.</param>
    /// <returns>The track, or <see langword="null"/> when the address is outside the disc.</returns>
    DiscTrack? FindTrack(int lba)
    {
        foreach (DiscTrack track in Tracks)
        {
            if (track.Contains(lba))
            {
                return track;
            }
        }

        return null;
    }
}
