using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Storage.Chd;

namespace ThreeDo.Storage;

/// <summary>
/// Opens 3DO disc images and presents them through the common <see cref="IDiscSource"/> API.
/// </summary>
public static class DiscImageFactory
{
    /// <summary>Opens a disc image by path and detected container format.</summary>
    /// <param name="path">Path to an ISO, BIN, CUE, or CHD image.</param>
    /// <param name="chdParentPath">Optional parent CHD path for a differential CHD.</param>
    /// <param name="prefetch">Whether to add a host-side read-ahead cache.</param>
    /// <param name="prefetchAhead">Sectors to read ahead when prefetching is enabled.</param>
    /// <param name="prefetchCapacity">Maximum cached sectors when prefetching is enabled.</param>
    /// <returns>An open disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">Thrown when the image cannot be opened or its format is unsupported.</exception>
    public static IDiscSource Open(
        string path,
        string? chdParentPath = null,
        bool prefetch = false,
        int prefetchAhead = 32,
        int prefetchCapacity = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new DiscImageException($"Disc image '{path}' does not exist.");
        }

        var source = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cue" => CueSheet.Open(path),
            ".chd" => ChdDiscImage.Open(path, chdParentPath),
            ".iso" => new IsoImage(path),
            ".bin" => OpenBin(path),
            _ => OpenByHeader(path, chdParentPath),
        };

        if (!prefetch)
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
        using var stream = File.OpenRead(path);
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

    private static IDiscSource OpenByHeader(string path, string? chdParentPath)
    {
        Span<byte> signature = stackalloc byte[8];
        using (var stream = File.OpenRead(path))
        {
            if (stream.Read(signature) < signature.Length)
            {
                throw new DiscImageException($"Disc image '{path}' is too small.");
            }
        }

        if (signature.SequenceEqual("MComprHD"u8))
        {
            return ChdDiscImage.Open(path, chdParentPath);
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
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[16];
        if (stream.Read(header) < header.Length)
        {
            return 1;
        }

        return header[15] == 2 ? 2 : 1;
    }
}
