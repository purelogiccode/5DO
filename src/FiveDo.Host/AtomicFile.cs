using System;
using System.IO;
using FiveDo.Diagnostics.Logging;

namespace FiveDo.Host;

/// <summary>
/// Writes a file through a same-directory temporary file and an atomic replace.
/// </summary>
/// <remarks>
/// Cloud-sync providers and processes that hold a file open without delete sharing can
/// reject the atomic replace even when the destination is writable. The writer then copies
/// the completed temporary file over the destination, which keeps the new contents at the
/// cost of atomicity. Failures are logged and reported to the caller instead of escaping.
/// </remarks>
internal static class AtomicFile
{
    /// <summary>Writes text to a file, preferring an atomic replace.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="contents">Complete file contents.</param>
    /// <param name="description">Short description used in log messages.</param>
    /// <returns><see langword="true"/> when the file was written; otherwise <see langword="false"/>.</returns>
    public static bool WriteAllText(string path, string contents, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var temporary = CreateTemporaryPath(path);
        try
        {
            CreateDirectory(path);
            File.WriteAllText(temporary, contents);
            return Replace(temporary, path, description);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogFailure(exception, path, description);
            return false;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>Writes bytes to a file, preferring an atomic replace.</summary>
    /// <param name="path">Destination path.</param>
    /// <param name="contents">Complete file contents.</param>
    /// <param name="description">Short description used in log messages.</param>
    /// <returns><see langword="true"/> when the file was written; otherwise <see langword="false"/>.</returns>
    public static bool WriteAllBytes(string path, ReadOnlySpan<byte> contents, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var temporary = CreateTemporaryPath(path);
        try
        {
            CreateDirectory(path);
            File.WriteAllBytes(temporary, contents);
            return Replace(temporary, path, description);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogFailure(exception, path, description);
            return false;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static bool Replace(string temporary, string path, string description)
    {
        try
        {
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The destination may be a cloud-sync placeholder or be held open without
            // delete sharing. Replacing its contents directly is not atomic, but it is
            // preferable to failing the save and aborting the caller.
            Serilog.Log.Debug(
                "Atomic replace for the {Description} at {Path} was denied; copying directly.",
                description,
                path);
            File.Copy(temporary, path, overwrite: true);
            return true;
        }
    }

    private static string CreateTemporaryPath(string path)
    {
        // A per-write name avoids collisions between the desktop shell and a CLI process
        // that share a data directory.
        return path + "." + Path.GetRandomFileName() + ".tmp";
    }

    private static void CreateDirectory(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void LogFailure(Exception exception, string path, string description)
    {
        Serilog.Log.ForContext(BugReportSink.SuppressProperty, false).Warning(
            exception,
            "The {Description} at {Path} could not be written.",
            description,
            path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "The temporary file {Path} could not be deleted.", path);
        }
    }
}
