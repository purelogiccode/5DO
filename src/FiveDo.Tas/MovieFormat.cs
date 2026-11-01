using System;

namespace FiveDo.Tas;

/// <summary>
/// Constants and limits for the 5DO TAS movie container.
/// </summary>
/// <remarks>
/// A movie stores one PBUS packet per emulated frame, an optional starting save state,
/// and a JSON metadata header. Packets are recorded as raw bytes so future input
/// devices do not change the movie format. The payload is Brotli-compressed and every
/// region is protected by a CRC-32.
/// </remarks>
public static class MovieFormat
{
    /// <summary>ASCII magic stored at the start of every movie file.</summary>
    public static ReadOnlySpan<byte> Magic => "5DOMOVIE"u8;

    /// <summary>Current movie format version.</summary>
    public const int Version = 1;

    /// <summary>Oldest movie format version this build can read.</summary>
    public const int MinSupportedVersion = 1;

    /// <summary>Size of the fixed movie file header in bytes.</summary>
    public const int FileHeaderSize = 52;

    /// <summary>Largest PBUS packet accepted by the recorder.</summary>
    public const int MaxPacketLength = 256;

    /// <summary>Largest uncompressed input payload accepted by the reader.</summary>
    public const int MaxUncompressedPayloadLength = 256 * 1024 * 1024;

    /// <summary>Largest embedded save state accepted by the reader.</summary>
    public const int MaxStateLength = 64 * 1024 * 1024;

    /// <summary>File extension used by 5DO movies.</summary>
    public const string Extension = ".5domovie";

    /// <summary>Checks whether a movie version is readable by this build.</summary>
    /// <param name="version">Version from the file header.</param>
    /// <returns><see langword="true"/> when the version is supported.</returns>
    public static bool IsSupportedVersion(int version)
    {
        return version >= MinSupportedVersion && version <= Version;
    }

    /// <summary>Checks whether a movie format version is the current writer version.</summary>
    /// <param name="version">Version from the file header.</param>
    /// <returns><see langword="true"/> when the version is current.</returns>
    public static bool IsCurrentVersion(int version)
    {
        return version == Version;
    }

    /// <summary>Validates a packet length.</summary>
    /// <param name="length">Packet length in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is invalid.</exception>
    public static void ValidatePacketLength(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxPacketLength);
    }
}
