using System;

namespace CDISharp;

/// <summary>Describes one track in a DiscJuggler image.</summary>
public sealed class CdiTrack
{
    internal CdiTrack(
        int number,
        int session,
        CdiTrackMode mode,
        CdiReadMode readMode,
        int startSector,
        int sectorCount,
        int sectorSize,
        int bytesPerSector,
        int payloadOffset,
        long fileOffset,
        int sourceStartSector,
        int pregapSectors,
        string? title,
        string? isrc,
        int control)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(session, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(startSector);
        ArgumentOutOfRangeException.ThrowIfNegative(sectorCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectorSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerSector);
        ArgumentOutOfRangeException.ThrowIfNegative(payloadOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceStartSector);
        ArgumentOutOfRangeException.ThrowIfNegative(pregapSectors);

        Number = number;
        Session = session;
        Mode = mode;
        ReadMode = readMode;
        StartSector = startSector;
        SectorCount = sectorCount;
        SectorSize = sectorSize;
        BytesPerSector = bytesPerSector;
        PayloadOffset = payloadOffset;
        FileOffset = fileOffset;
        SourceStartSector = sourceStartSector;
        PregapSectors = pregapSectors;
        Title = title;
        Isrc = isrc;
        Control = control;
    }

    /// <summary>Gets the one-based track number.</summary>
    public int Number { get; }

    /// <summary>Gets the one-based session number.</summary>
    public int Session { get; }

    /// <summary>Gets the track recording mode.</summary>
    public CdiTrackMode Mode { get; }

    /// <summary>Gets how DiscJuggler stored the track sectors.</summary>
    public CdiReadMode ReadMode { get; }

    /// <summary>Gets the first contiguous logical sector address of the track.</summary>
    public int StartSector { get; }

    /// <summary>Gets the number of sectors in the track.</summary>
    public int SectorCount { get; }

    /// <summary>Gets the native sector payload size: 2048, 2336, or 2352 bytes.</summary>
    public int SectorSize { get; }

    /// <summary>Gets the number of bytes occupied by one sector in the image file.</summary>
    public int BytesPerSector { get; }

    /// <summary>Gets the offset of 2048-byte user data inside a native sector.</summary>
    public int PayloadOffset { get; }

    /// <summary>Gets the byte offset of the first stored sector in the image file.</summary>
    public long FileOffset { get; }

    /// <summary>Gets the track start address recorded in the DiscJuggler descriptor.</summary>
    public int SourceStartSector { get; }

    /// <summary>Gets the number of pregap sectors recorded before track index 1.</summary>
    public int PregapSectors { get; }

    /// <summary>Gets the CD-Text track title, when one is present.</summary>
    public string? Title { get; }

    /// <summary>Gets the International Standard Recording Code, when one is present.</summary>
    public string? Isrc { get; }

    /// <summary>Gets the raw ADR/control flags recorded for the track.</summary>
    public int Control { get; }

    /// <summary>Gets the exclusive end logical sector address of the track.</summary>
    public int EndSector => StartSector + SectorCount;

    /// <summary>Gets whether the track contains CD-DA audio.</summary>
    public bool IsAudio => Mode == CdiTrackMode.Audio;

    /// <summary>Returns whether <paramref name="lba"/> belongs to this track.</summary>
    /// <param name="lba">Contiguous logical sector address.</param>
    /// <returns><see langword="true"/> when the address is inside the track.</returns>
    public bool Contains(int lba) => lba >= StartSector && lba < EndSector;
}
