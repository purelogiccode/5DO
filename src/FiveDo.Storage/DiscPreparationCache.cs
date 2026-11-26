using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace FiveDo.Storage;

/// <summary>
/// Caches the CUE sheets (and any prepared data files) generated for MDS and CloneCD
/// images, keyed by the source path, length, and last-write time.
/// </summary>
/// <remarks>
/// The converters are intentionally read-only and may need to join, strip, or decode an
/// image before it can be read as a CUE/BIN set. Re-running that work on every library
/// scan would be expensive, so the result lives under the process temp folder until the
/// source changes. Entries older than 30 days are removed on first use.
/// </remarks>
internal static class DiscPreparationCache
{
    internal const string PreparedCueName = "prepared.cue";
    internal const int CacheFormatVersion = 2;

    private const int RetentionDays = 30;
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "5do-disc-cache");
    private static int _cleanupStarted;

    /// <summary>Gets the lock used to serialize first-time preparation of a source.</summary>
    internal static object Sync { get; } = new();

    /// <summary>Gets the cache directory for one source file and format.</summary>
    /// <param name="sourcePath">Source disc image path.</param>
    /// <param name="format">Short format name, such as <c>mds</c> or <c>ccd</c>.</param>
    /// <param name="dataPaths">Companion data files whose identity must invalidate the cache.</param>
    /// <returns>An existing or newly created cache directory.</returns>
    internal static string GetDirectory(
        string sourcePath,
        string format,
        IEnumerable<string>? dataPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var identity = new StringBuilder();
        identity.Append('v').Append(CacheFormatVersion).Append('|');
        AppendFileIdentity(identity, sourcePath);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(sourcePath),
        };
        if (dataPaths is not null)
        {
            foreach (string dataPath in dataPaths)
            {
                if (string.IsNullOrWhiteSpace(dataPath))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(dataPath);
                if (seen.Add(fullPath))
                {
                    AppendFileIdentity(identity, fullPath);
                }
            }
        }

        identity.Append('|').Append(format);
        string hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())))
            .ToLowerInvariant()[..16];
        string directory = Path.Combine(Root, format, hash);
        Directory.CreateDirectory(directory);
        CleanupOnce();
        return directory;
    }

    private static void AppendFileIdentity(StringBuilder builder, string path)
    {
        builder.Append(Path.GetFullPath(path)).Append('|');
        try
        {
            var info = new FileInfo(path);
            if (info.Exists)
            {
                builder.Append(info.Length)
                    .Append('|')
                    .Append(info.LastWriteTimeUtc.Ticks);
            }
            else
            {
                builder.Append("missing");
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            builder.Append("unreadable");
        }

        builder.Append('|');
    }

    /// <summary>Finds a prepared CUE in a cache directory.</summary>
    /// <param name="directory">Cache directory.</param>
    /// <returns>The CUE path, or <see langword="null"/> when no prepared CUE exists.</returns>
    internal static string? FindPreparedCue(string directory)
    {
        string cuePath = Path.Combine(directory, PreparedCueName);
        return File.Exists(cuePath) ? cuePath : null;
    }

    /// <summary>Publishes a generated CUE as the cache directory's prepared CUE.</summary>
    /// <param name="cuePath">Generated CUE path.</param>
    /// <param name="directory">Cache directory.</param>
    internal static void PublishCue(string cuePath, string directory)
    {
        string destination = Path.Combine(directory, PreparedCueName);
        if (string.Equals(cuePath, destination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(cuePath, temporary, overwrite: false);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Removes a cache directory after a failed open.</summary>
    /// <param name="directory">Cache directory to remove.</param>
    internal static void Invalidate(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A locked cache entry is harmless; the next open can retry it.
        }
    }

    private static void CleanupOnce()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        try
        {
            if (!Directory.Exists(Root))
            {
                return;
            }

            foreach (string formatDirectory in Directory.EnumerateDirectories(Root))
            {
                foreach (string directory in Directory.EnumerateDirectories(formatDirectory))
                {
                    try
                    {
                        if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                        {
                            Directory.Delete(directory, recursive: true);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        // Another process may still own the entry.
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The cache is best-effort; preparation still works without cleanup.
        }
    }
}
