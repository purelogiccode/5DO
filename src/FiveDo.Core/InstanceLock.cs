using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FiveDo.Core;

/// <summary>
/// A cross-process lock that prevents two instances from writing the same data directory.
/// </summary>
/// <remarks>
/// <para>
/// The lock is an exclusively opened <c>5do.instance.lock</c> file in the data directory.
/// The file records the owning process ID, start time, and version so a second launch can
/// explain the conflict. Disposing the lock closes and removes the file.
/// </para>
/// <para>
/// Automated runs can bypass the check with <c>FIVEDO_ALLOW_MULTIPLE=1</c>. Separate
/// portable directories or named profiles use separate lock files and may run together.
/// </para>
/// </remarks>
public sealed class InstanceLock : IDisposable
{
    /// <summary>The lock file name.</summary>
    public const string FileName = "5do.instance.lock";

    /// <summary>The environment variable that disables the single-instance check.</summary>
    public const string BypassEnvironmentVariable = "FIVEDO_ALLOW_MULTIPLE";

    private FileStream? _stream;
    private bool _disposed;

    private InstanceLock(FileStream stream, string path)
    {
        _stream = stream;
        Path = path;
    }

    /// <summary>Gets the full lock file path.</summary>
    public string Path { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the current process should skip the lock check.
    /// </summary>
    public static bool IsBypassed =>
        string.Equals(
            Environment.GetEnvironmentVariable(BypassEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

    /// <summary>Attempts to acquire the data-directory lock.</summary>
    /// <param name="directory">Data directory to lock.</param>
    /// <param name="instanceLock">The acquired lock, or <see langword="null"/> on conflict.</param>
    /// <param name="conflict">A description of the owning process, or the failure reason.</param>
    /// <returns><see langword="true"/> when the lock was acquired.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="directory"/> is empty.</exception>
    public static bool TryAcquire(string directory, out InstanceLock? instanceLock, out string? conflict)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullDirectory = System.IO.Path.GetFullPath(directory);
        var path = System.IO.Path.Combine(fullDirectory, FileName);
        instanceLock = null;
        try
        {
            Directory.CreateDirectory(fullDirectory);
            var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 128,
                FileOptions.WriteThrough);
            try
            {
                stream.SetLength(0);
                var text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Environment.ProcessId}|{DateTimeOffset.UtcNow:O}|{FiveDoVersion.Version}");
                var bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            conflict = null;
            instanceLock = new InstanceLock(stream, path);
            return true;
        }
        catch (IOException exception)
        {
            conflict = DescribeOwner(path, exception);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            conflict = $"The data directory {fullDirectory} is not writable: {exception.Message}";
            return false;
        }
    }

    /// <summary>Reads the lock file's owner description without throwing.</summary>
    /// <param name="path">Lock file path.</param>
    /// <returns>A human-readable description, or <see langword="null"/> when the file is absent.</returns>
    public static string? ReadOwner(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd().Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FileStream? stream = _stream;
        _stream = null;
        if (stream is not null)
        {
            stream.Dispose();
        }

        try
        {
            File.Delete(Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The operating system releases the lock when the process exits; a stale
            // file is harmless because opening it exclusively still detects a live owner.
        }
    }

    private static string DescribeOwner(string path, IOException exception)
    {
        var owner = ReadOwner(path);
        if (string.IsNullOrWhiteSpace(owner))
        {
            return $"Another 5DO instance is using this data directory ({exception.Message}).";
        }

        return $"Another 5DO instance is using this data directory: {owner}.";
    }
}
