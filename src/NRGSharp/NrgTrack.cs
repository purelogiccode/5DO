using System;

namespace NRGSharp;

/// <summary>Describes one track in a Nero NRG image.</summary>
public sealed class NrgTrack
{
    internal NrgTrack(
        int number,
        int session,
        NrgTrackMode mode,
        int startSector,
        int sectorCount,
        int sectorSize,
        int bytesPerSector,
        int payloadOffset,
        long fileOffset,
        string? isrc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(session, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(startSector);
        ArgumentOutOfRangeException.ThrowIfNegative(sectorCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectorSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerSector);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);

        Number = number;
        Session = session;
        Mode = mode;
        StartSector = startSector;
        SectorCount = sectorCount;
        SectorSize = sectorSize;
        BytesPerSector = bytesPerSector;
        PayloadOffset = payloadOffset;
        FileOffset = fileOffset;
        Isrc = isrc;
    }

    /// <summary>Gets the one-based track number.</summary>
    public int Number { get; }

    /// <summary>Gets the one-based session number.</summary>
    public int Session { get; internal set; }

    /// <summary>Gets the track recording mode.</summary>
    public NrgTrackMode Mode { get; }

    /// <summary>Gets the first contiguous logical sector address of the track.</summary>
    public int StartSector { get; }

    /// <summary>Gets the number of sectors in the track.</summary>
    public int SectorCount { get; }

    /// <summary>Gets the native sector payload size: 2048, 2324, 2336, or 2352 bytes.</summary>
    public int SectorSize { get; }

    /// <summary>Gets the number of bytes occupied by one sector in the image file.</summary>
    public int BytesPerSector { get; }

    /// <summary>Gets the offset of 2048-byte user data inside a native sector.</summary>
    public int PayloadOffset { get; }

    /// <summary>Gets the byte offset of the first track sector in the image file.</summary>
    public long FileOffset { get; }

    /// <summary>Gets the International Standard Recording Code, when one is present.</summary>
    public string? Isrc { get; }

    /// <summary>Gets the exclusive end logical sector address of the track.</summary>
    public int EndSector => StartSector + SectorCount;

    /// <summary>Gets whether the track contains CD-DA audio.</summary>
    public bool IsAudio => Mode is NrgTrackMode.Audio or NrgTrackMode.AudioAlt or NrgTrackMode.AudioSubchannel;

    /// <summary>Returns whether <paramref name="lba"/> belongs to this track.</summary>
    /// <param name="lba">Contiguous logical sector address.</param>
    /// <returns><see langword="true"/> when the address is inside the track.</returns>
    public bool Contains(int lba) => lba >= StartSector && lba < EndSector;
}
