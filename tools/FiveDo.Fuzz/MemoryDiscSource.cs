using System;
using FiveDo.Core;

namespace FiveDo.Fuzz;

/// <summary>A read-only in-memory 2048-byte-sector disc source.</summary>
internal sealed class MemoryDiscSource : IDiscSource
{
    private readonly byte[] _image;

    /// <summary>Initializes a disc source over an image.</summary>
    /// <param name="image">Image bytes. A partial trailing sector is ignored.</param>
    internal MemoryDiscSource(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _image = image;
    }

    /// <inheritdoc />
    public int SectorCount => _image.Length / 2048;

    /// <inheritdoc />
    public int SectorSize => 2048;

    /// <inheritdoc />
    public void ReadSector(int lba, Span<byte> destination)
    {
        if (destination.Length < SectorSize)
        {
            throw new ArgumentException("The destination must hold a full sector.", nameof(destination));
        }

        if (lba < 0 || lba >= SectorCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lba), lba, "The sector is outside the image.");
        }

        _image.AsSpan(lba * SectorSize, SectorSize).CopyTo(destination);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
