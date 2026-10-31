using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// A raw (2352-byte) CD image, with optional track start offset and payload offset.
/// </summary>
/// <remarks>
/// 3DO only reads the 2048-byte user data from each sector. Mode 1 stores it at byte 16 and
/// Mode 2 Form 1 at byte 24; <see cref="DiscImageFactory"/> selects the offset from the image's
/// mode byte or the CUE sheet.
/// </remarks>
public sealed class BinImage : IDiscSource
{
    /// <summary>Raw sector size.</summary>
    public const int RawSectorBytes = 2352;

    private readonly FileStream _stream;
    private readonly long _startOffset;
    private readonly int _payloadOffset;
    private readonly byte[] _sector = new byte[RawSectorBytes];

    /// <summary>Initializes a new instance of the <see cref="BinImage"/> class.</summary>
    /// <param name="path">Path to the BIN file.</param>
    /// <param name="startOffset">Byte offset of the first track's sector 0.</param>
    /// <param name="sectorCount">Number of sectors to expose.</param>
    /// <param name="payloadOffset">Byte offset of user data within each raw sector (16 or 24).</param>
    public BinImage(string path, long startOffset = 0, int? sectorCount = null, int payloadOffset = 16)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);

        if (payloadOffset is not (16 or 24))
        {
            throw new ArgumentOutOfRangeException(nameof(payloadOffset), payloadOffset, "Payload offset must be 16 or 24.");
        }

        _stream = File.OpenRead(path);
        _startOffset = startOffset;
        _payloadOffset = payloadOffset;

        var remaining = _stream.Length - startOffset;
        if (remaining < 0 || remaining % RawSectorBytes != 0)
        {
            _stream.Dispose();
            throw new DiscImageException($"BIN file '{path}' does not contain whole 2352-byte sectors from offset {startOffset}.");
        }

        var available = (int)(remaining / RawSectorBytes);
        SectorCount = sectorCount.HasValue ? Math.Min(sectorCount.Value, available) : available;
    }

    /// <inheritdoc />
    public int SectorCount { get; }

    /// <inheritdoc />
    public int SectorSize => 2048;

    /// <inheritdoc />
    public void ReadSector(int lba, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        if (lba >= SectorCount)
        {
            throw new DiscImageException($"Sector {lba} is outside the image ({SectorCount} sectors).");
        }

        _stream.Position = _startOffset + ((long)lba * RawSectorBytes);
        _stream.ReadExactly(_sector);
        var available = Math.Min(2048, destination.Length);
        _sector.AsSpan(_payloadOffset, available).CopyTo(destination);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stream.Dispose();
    }
}
