using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Storage.Chd;

namespace FiveDo.Storage;

/// <summary>
/// Opens 3DO disc images and presents them through the common <see cref="IDiscSource"/> API.
/// </summary>
public static class DiscImageFactory
{
    /// <summary>Opens a disc image by path and detected container format.</summary>
    /// <param name="path">Path to an ISO, BIN, CUE, or CHD image.</param>
    /// <param name="chdParentPath">Optional parent CHD path for a differential CHD.</param>
    /// <param name="prefetch">
    /// Whether to add a host-side read-ahead cache for uncompressed image readers.
    /// CHD sources use CHDSharp's own hunk read-ahead directly.
    /// </param>
    /// <param name="prefetchAhead">Sectors to read ahead when prefetching is enabled.</param>
    /// <param name="prefetchCapacity">Maximum cached sectors when prefetching is enabled.</param>
    /// <param name="chdReadAheadHunks">
    /// CHD hunks to decompress in the background. Zero disables CHD read-ahead for
    /// metadata-only callers such as the game-library scanner.
    /// </param>
    /// <returns>An open disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">Thrown when the image cannot be opened or its format is unsupported.</exception>
    public static IDiscSource Open(
        string path,
        string? chdParentPath = null,
        bool prefetch = false,
        int prefetchAhead = 32,
        int prefetchCapacity = 128,
        int chdReadAheadHunks = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (PhysicalDiscSource.IsOpticalDrivePath(path))
        {
            // A drive root is not a file; the cooked block-device reader presents the same
            // 2048-byte user-data sectors as an image. Read-ahead is skipped because a
            // disc change or media error must be reported immediately.
            return PhysicalDiscSource.Open(path);
        }

        if (!File.Exists(path))
        {
            throw new DiscImageException($"Disc image '{path}' does not exist.");
        }

        IDiscSource source = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cue" => CueSheet.Open(path),
            ".chd" => ChdDiscImage.Open(path, chdParentPath, chdReadAheadHunks),
            ".iso" => OpenIso(path),
            ".bin" => OpenBin(path),
            _ => OpenByHeader(path, chdParentPath, chdReadAheadHunks),
        };

        if (!prefetch)
        {
            return source;
        }

        // CHDSharp already decompresses upcoming hunks on its own read-ahead thread.
        // Wrapping it in the sector prefetcher adds a second look-ahead that holds the
        // inner-reader lock, and foreground CD reads can then queue behind it for
        // several frames. Keep the sector cache for images whose reader is just a file.
        if (source is ChdDiscImage)
        {
            return source;
        }

        try
        {
            return new PrefetchingDiscSource(source, prefetchAhead, prefetchCapacity);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>Detects whether a file looks like a raw 2352-byte-sector BIN image.</summary>
    /// <param name="path">Path to inspect.</param>
    /// <returns><see langword="true"/> when the file starts with a Mode 1/2 sync pattern.</returns>
    public static bool HasCdSyncHeader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        if (stream.Read(header) < header.Length)
        {
            return false;
        }

        return header[0] == 0x00
            && header[1] == 0xFF
            && header[2] == 0xFF
            && header[3] == 0xFF
            && header[4] == 0xFF
            && header[5] == 0xFF
            && header[6] == 0xFF
            && header[7] == 0xFF
            && header[8] == 0xFF
            && header[9] == 0xFF
            && header[10] == 0xFF
            && header[11] == 0x00;
    }

    private static IDiscSource OpenIso(string path)
    {
        var length = new FileInfo(path).Length;

        // Raw 2352-byte-sector dumps are often named .iso. Check the CD sync pattern before
        // the cooked-sector length, since an exact multiple of 2352 can also divide by 2048.
        if (length % BinImage.RawSectorBytes == 0 && HasCdSyncHeader(path))
        {
            var mode = ReadModeByte(path);
            return new BinImage(path, payloadOffset: mode == 2 ? 24 : 16);
        }

        if (length % IsoImage.SectorBytes == 0)
        {
            return new IsoImage(path);
        }

        throw new DiscImageException(
            $"ISO file '{path}' is {length:N0} bytes, which is not a whole number of 2048-byte "
            + $"3DO sectors (remainder {length % IsoImage.SectorBytes:N0}). It may be a raw "
            + "2352-byte-sector dump without a complete CD sync header, or an incomplete download.");
    }

    private static IDiscSource OpenBin(string path)
    {
        if (!HasCdSyncHeader(path))
        {
            var length = new FileInfo(path).Length;
            if (length % IsoImage.SectorBytes == 0)
            {
                return new IsoImage(path);
            }

            throw new DiscImageException($"BIN file '{path}' has neither a CD sync header nor 2048-byte sectors.");
        }

        var mode = ReadModeByte(path);
        var payloadOffset = mode == 2 ? 24 : 16;
        return new BinImage(path, payloadOffset: payloadOffset);
    }

    private static IDiscSource OpenByHeader(string path, string? chdParentPath, int chdReadAheadHunks)
    {
        Span<byte> signature = stackalloc byte[8];
        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.Read(signature) < signature.Length)
            {
                throw new DiscImageException($"Disc image '{path}' is too small.");
            }
        }

        if (signature.SequenceEqual("MComprHD"u8))
        {
            return ChdDiscImage.Open(path, chdParentPath, chdReadAheadHunks);
        }

        if (HasCdSyncHeader(path))
        {
            return OpenBin(path);
        }

        var length = new FileInfo(path).Length;
        if (length % IsoImage.SectorBytes == 0)
        {
            return new IsoImage(path);
        }

        throw new DiscImageException($"Cannot determine the disc image format of '{path}'.");
    }

    private static int ReadModeByte(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        if (stream.Read(header) < header.Length)
        {
            return 1;
        }

        return header[15] == 2 ? 2 : 1;
    }
}
