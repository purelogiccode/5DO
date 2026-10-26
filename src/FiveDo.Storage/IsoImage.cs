using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// A 3DO disc image backed by a file of 2048-byte user-data sectors (ISO).
/// </summary>
public sealed class IsoImage : IDiscSource
{
    private readonly FileStream _stream;
    private readonly byte[] _sector = new byte[SectorBytes];

    /// <summary>Gets the size of one user-data sector.</summary>
    public const int SectorBytes = 2048;

    /// <summary>Initializes a new instance of the <see cref="IsoImage"/> class.</summary>
    /// <param name="path">Path to the ISO file.</param>
    /// <exception cref="DiscImageException">Thrown when the file size is not a whole number of sectors.</exception>
    public IsoImage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _stream = File.OpenRead(path);
        if (_stream.Length % SectorBytes != 0)
        {
            _stream.Dispose();
            throw new DiscImageException($"ISO file '{path}' is not a whole number of 2048-byte sectors.");
        }

        SectorCount = (int)(_stream.Length / SectorBytes);
    }

    /// <inheritdoc />
    public int SectorCount { get; }

    /// <inheritdoc />
    public int SectorSize => SectorBytes;

    /// <inheritdoc />
    public void ReadSector(int lba, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        if (lba >= SectorCount)
        {
            throw new DiscImageException($"Sector {lba} is outside the image ({SectorCount} sectors).");
        }

        _stream.Position = (long)lba * SectorBytes;
        var read = _stream.ReadAtLeast(_sector, SectorBytes, throwOnEndOfStream: false);
        var copy = Math.Min(read, destination.Length);
        _sector.AsSpan(0, copy).CopyTo(destination);
    }

    /// <inheritdoc />
    public void Dispose() => _stream.Dispose();
}
