using System;

namespace FiveDo.Core;

/// <summary>
/// A read-only source of 2048-byte 3DO data sectors.
/// </summary>
/// <remarks>
/// The 3DO CD-ROM device and the game database only need user-data sectors, not raw
/// 2352-byte frames or subcode. Implementations translate their container format
/// (ISO, BIN, CUE+BIN, CHD) to this interface.
/// </remarks>
public interface IDiscSource : IDisposable
{
    /// <summary>Gets the number of 2048-byte user-data sectors.</summary>
    int SectorCount { get; }

    /// <summary>Gets the user-data sector size in bytes. This is always 2048 for 3DO discs.</summary>
    int SectorSize { get; }

    /// <summary>Reads one 2048-byte user-data sector into <paramref name="destination"/>.</summary>
    /// <param name="lba">Logical block address, zero-based.</param>
    /// <param name="destination">Destination span of at least <see cref="SectorSize"/> bytes.</param>
    void ReadSector(int lba, Span<byte> destination);
}
