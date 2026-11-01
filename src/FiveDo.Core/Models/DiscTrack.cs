namespace FiveDo.Core;

/// <summary>Identifies the payload of a CD track.</summary>
public enum DiscTrackKind
{
    /// <summary>A data track (Mode 1 or Mode 2).</summary>
    Data,

    /// <summary>A Red Book audio track.</summary>
    Audio,
}

/// <summary>
/// Describes one track on a CD image using logical disc block addresses.
/// </summary>
/// <remarks>
/// Logical block address 0 is the first data track's INDEX 01 position (MSF 00:02:00),
/// matching the 3DO CD-ROM device. Audio tracks that follow a data track occupy the
/// logical range after the data track, so a READ_DATA request can address them by LBA.
/// </remarks>
/// <param name="Number">One-based track number.</param>
/// <param name="Kind">Data or audio.</param>
/// <param name="StartLba">First logical block address belonging to the track.</param>
/// <param name="SectorCount">Number of sectors in the track.</param>
/// <param name="SectorSize">Native payload size in bytes (2048, 2336, or 2352).</param>
/// <param name="PayloadOffset">
/// Byte offset of the 2048-byte user data inside the native sector. Zero for cooked
/// sectors and audio, 16 for Mode 1 raw, and 24 for Mode 2 raw.
/// </param>
public sealed record DiscTrack(
    int Number,
    DiscTrackKind Kind,
    int StartLba,
    int SectorCount,
    int SectorSize,
    int PayloadOffset = 0)
{
    /// <summary>Gets the exclusive end logical block address of the track.</summary>
    public int EndLba => StartLba + SectorCount;

    /// <summary>Gets whether the track contains CD audio.</summary>
    public bool IsAudio => Kind == DiscTrackKind.Audio;

    /// <summary>Returns whether <paramref name="lba"/> belongs to this track.</summary>
    /// <param name="lba">Logical block address.</param>
    public bool Contains(int lba) => lba >= StartLba && lba < EndLba;
}
