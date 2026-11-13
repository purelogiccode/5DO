using System;
using System.IO;

namespace FiveDo.Host;

/// <summary>
/// Reads file identity fields that some file systems and network shares report with an
/// invalid timestamp. <see cref="FileSystemInfo.LastWriteTimeUtc"/> throws
/// <see cref="ArgumentOutOfRangeException"/> for such files (the underlying Win32
/// FILETIME cannot be converted to a <see cref="DateTime"/>), and that failure must not
/// abort a library scan.
/// </summary>
internal static class FileStamp
{
    /// <summary>
    /// Sentinel stored in a cache record when the file system does not report a usable
    /// last-write time. A real timestamp can never use this value because ticks are
    /// non-negative.
    /// </summary>
    internal const long UnknownTicks = -1;

    /// <summary>Tries to read the last-write time without throwing for invalid values.</summary>
    /// <param name="info">File information to read.</param>
    /// <param name="value">The last-write time, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> when the timestamp could be read.</returns>
    internal static bool TryGetLastWriteTimeUtc(FileInfo info, out DateTime value)
    {
        ArgumentNullException.ThrowIfNull(info);
        try
        {
            value = info.LastWriteTimeUtc;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Reads the last-write time in UTC ticks, or <see cref="UnknownTicks"/> when the file
    /// system does not report a valid timestamp.
    /// </summary>
    /// <param name="info">File information to read.</param>
    /// <returns>The UTC ticks, or <see cref="UnknownTicks"/>.</returns>
    internal static long GetLastWriteTimeUtcTicks(FileInfo info)
    {
        return TryGetLastWriteTimeUtc(info, out DateTime value) ? value.Ticks : UnknownTicks;
    }

    /// <summary>
    /// Compares a file with a cached size/timestamp pair. A file whose timestamp cannot be
    /// read only matches a record that was also created without a timestamp, so a file
    /// that starts reporting a valid time is re-inspected once.
    /// </summary>
    /// <param name="info">Current file information.</param>
    /// <param name="length">Cached file length.</param>
    /// <param name="ticks">Cached UTC ticks, or <see cref="UnknownTicks"/>.</param>
    /// <returns><see langword="true"/> when the cached identity still matches.</returns>
    internal static bool Matches(FileInfo info, long length, long ticks)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Length != length)
        {
            return false;
        }

        return TryGetLastWriteTimeUtc(info, out DateTime value)
            ? value.Ticks == ticks
            : ticks == UnknownTicks;
    }
}
